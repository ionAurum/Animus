using Animus.Core.Agents;
using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// The core simulation loop. Advances the world one tick at a time.
///
/// Four phases execute in order. All agents complete each phase
/// before the next phase begins, enabling parallel execution within phases.
///
/// Phase 1 — Read and Compute (parallel, read-only)
/// Phase 2 — Declare (parallel, intentions visible, no state changes)
/// Phase 3 — Resolve (sequential/arbitrated, state changes happen here)
/// Phase 4 — Integrate (parallel, final writes)
///
/// The algorithm is closed — new complexity lives in the atom space,
/// never in new steps.
/// </summary>
public sealed class SimulationLoop
{
    private readonly IEnvironment _environment;
    private readonly List<Agent> _agents = [];
    private int _tick = 0;

    public int Tick => _tick;
    public IReadOnlyList<Agent> Agents => _agents;

    public SimulationLoop(IEnvironment environment, IEnumerable<Agent> agents)
    {
        _environment = environment;
        _agents.AddRange(agents);
    }

    /// <summary>
    /// Advances the simulation by one tick.
    /// </summary>
    public void Step()
    {
        _tick++;

        // ── Phase 1 — Read & Compute ─────────────────────────────────────────
        var agentStates = _agents
            .Select(agent => ComputeAgentState(agent))
            .ToList();

        // ── Phase 2 — Declare ────────────────────────────────────────────────
        var intents = agentStates
            .SelectMany(state => DeclareIntent(state))
            .ToList();

        // ── Phase 3 — Resolve ────────────────────────────────────────────────
        var resolutions = Resolve(intents);

        foreach (var resolution in resolutions)
            _environment.ApplyResolution(resolution);

        // ── Phase 4 — Integrate ──────────────────────────────────────────────
        var updatedAgents = agentStates
            .Select(state => Integrate(state, resolutions))
            .ToList();

        _agents.Clear();
        _agents.AddRange(updatedAgents);

        _environment.OnTickComplete(_agents, _tick);
    }

    // ── Phase 1 ──────────────────────────────────────────────────────────────

    private AgentTickState ComputeAgentState(Agent agent)
    {
        // Step 1 — Apply decay (state, modifiers, memories, belief frames) + learning
        var decayedAgent = ApplyDecay(agent);

        // Step 2 — Receive perception tree from environment
        var perceptionTree = _environment.GetPerceptionTree(decayedAgent);

        // Step 3 — Interpret perception tree
        var interpretedContext = InterpretPerceptionTree(decayedAgent, perceptionTree);

        // Step 4 — Update beliefs
        var updatedAgent = UpdateBeliefs(decayedAgent, interpretedContext);

        // Step 5 — Compute urgency profile
        var urgencyModifierSums = ComputeUrgencyModifierSums(updatedAgent);
        var urgencyProfile = UrgencyFunction.ComputeProfile(
            updatedAgent.State,
            updatedAgent.Preference,
            updatedAgent.Urgency,
            urgencyModifierSums);

        // Step 6 — Score available advertisements
        var available = _environment.GetAvailableAdvertisements(updatedAgent);
        var scoredActions = ScoreAvailableActions(updatedAgent, urgencyProfile, interpretedContext, available);

        return new AgentTickState(
            Agent: updatedAgent,
            PerceptionTree: perceptionTree,
            InterpretedContext: interpretedContext,
            UrgencyProfile: urgencyProfile,
            ScoredActions: scoredActions);
    }

    private static Agent ApplyDecay(Agent agent)
    {
        // Decay state atoms — prefer state-specific decay frame, fall back to general decay frame
        // State-specific: context contains both ("decay", 1f) and ("state", 1f)
        // General: context contains only ("decay", 1f) — backwards compatible
        var stateDecayFrame = agent.Frames
            .FirstOrDefault(f => f.Context.Contains(CollectionRoles.Decay)
                              && f.Context.Contains(CollectionRoles.State))
            ?? agent.Frames
                .FirstOrDefault(f => f.Context.Contains(CollectionRoles.Decay)
                                  && !f.Context.Contains(CollectionRoles.Belief)
                                  && !f.Context.Contains(CollectionRoles.Modifier)
                                  && !f.Context.Contains(CollectionRoles.Memory));

        var decayedState = new AtomSet(
            agent.State.Atoms.Select(a =>
            {
                var rate = stateDecayFrame?.GetValue(a.Label) ?? 0f;
                var newMag = rate == 0f ? a.Magnitude : a.Magnitude + rate;
                // Clamp state atoms to [0, 2] for stability
                // Prevents unbounded drift in either direction
                return a with { Magnitude = Math.Clamp(newMag, 0f, 2f) };
            }));

        // Decay modifiers
        var decayedModifiers = agent.Modifiers
            .Select(m => m.ApplyDecay())
            .Where(m => !m.IsExpired)
            .ToList();

        // Decay memories
        var decayedMemories = agent.Memories
            .Select(m => m.ApplyDecay())
            .Where(m => !m.IsExpired)
            .ToList();

        // Apply learning — nudge belief frames from active memories each tick
        var agentAfterLearning = ApplyLearning(agent, decayedMemories);

        // Decay belief frames
        var beliefDecayFrame = agent.Frames
            .FirstOrDefault(f => f.Context.Contains(CollectionRoles.Decay)
                              && f.Context.Contains(CollectionRoles.Belief));

        var beliefDecayRate = beliefDecayFrame?.Default ?? 0.001f;

        var decayedBeliefFrames = agentAfterLearning.BeliefFrames
            .Select(f => DecayBeliefFrame(f, beliefDecayRate))
            .Where(f => f.Value.Atoms.Any(a => MathF.Abs(a.Magnitude) > float.Epsilon)
                     || f.Default.HasValue)
            .ToList();

        var result = agent.WithUpdatedCollection(CollectionRoles.State, decayedState);
        result = result.WithReplacedModifiers(decayedModifiers);
        result = result.WithReplacedMemories(decayedMemories);
        result = result.WithReplacedBeliefFrames(decayedBeliefFrames);
        return result;
    }

    private static Frame DecayBeliefFrame(Frame frame, float rate)
    {
        var decayedAtoms = frame.Value.Atoms.Select(a =>
        {
            var newMag = a.Magnitude > 0f
                ? MathF.Max(0f, a.Magnitude - rate)
                : MathF.Min(0f, a.Magnitude + rate);
            return a with { Magnitude = newMag };
        });

        var newDefault = frame.Default.HasValue
            ? (float?)MathF.Max(0f, frame.Default.Value - rate)
            : null;

        return frame with { Value = new AtomSet(decayedAtoms), Default = newDefault };
    }

    private static Agent ApplyLearning(Agent agent, List<Memory> memories)
    {
        if (memories.Count == 0) return agent;

        // Per-atom learning rates
        var learningRateFrame = agent.Frames
            .FirstOrDefault(f => f.Context.Contains(CollectionRoles.LearningRate));
        var defaultLearningRate = learningRateFrame?.Default ?? 0.01f;

        var updatedFrames = agent.BeliefFrames.ToList();

        foreach (var memory in memories)
        {
            foreach (var valueAtom in memory.Frame.Value.Atoms)
            {
                var learningRate = learningRateFrame?.GetValue(valueAtom.Label)
                                   is float r and > 0f ? r : defaultLearningRate;

                var delta = valueAtom.Magnitude
                          * memory.Recency
                          * memory.Significance
                          * learningRate;

                if (MathF.Abs(delta) < float.Epsilon) continue;

                // Find matching belief frame by context similarity
                var matchingFrame = updatedFrames
                    .FirstOrDefault(f => SimilarityFunction.ComputeContextMatch(f, memory.Frame.Context) > 0f);

                if (matchingFrame is not null)
                {
                    // Nudge existing frame value atom
                    var updatedAtoms = matchingFrame.Value.With(
                        new Atom(valueAtom.Label,
                            matchingFrame.Value[valueAtom.Label] + delta));
                    updatedFrames.Remove(matchingFrame);
                    updatedFrames.Add(matchingFrame with { Value = updatedAtoms });
                }
                else
                {
                    // Create new belief frame from memory context and delta
                    updatedFrames.Add(new Frame(
                        memory.Frame.Context,
                        new AtomSet(new Atom(valueAtom.Label, delta))));
                }
            }
        }

        return agent.WithReplacedBeliefFrames(updatedFrames);
    }

    private static AtomSet InterpretPerceptionTree(Agent agent, Observation? tree)
    {
        if (tree is null) return AtomSet.Empty;
        var subjectAtoms = tree.Subject.ToAtomSet();
        return BeliefFunction.ComputeContextualContribution(agent, subjectAtoms);
    }

    private static Agent UpdateBeliefs(Agent agent, AtomSet interpretedContext)
    {
        return agent;
    }

    private static AtomSet ComputeUrgencyModifierSums(Agent agent)
    {
        var sums = new Dictionary<string, float>();
        foreach (var modifier in agent.Modifiers.Where(m => !m.IsExpired))
        {
            foreach (var atom in modifier.CurrentValue.Atoms)
            {
                sums[atom.Label] = sums.TryGetValue(atom.Label, out var existing)
                    ? existing + atom.Magnitude
                    : atom.Magnitude;
            }
        }
        return new AtomSet(sums.Select(kvp => new Atom(kvp.Key, kvp.Value)));
    }

    private static List<ScoredAction> ScoreAvailableActions(
        Agent agent,
        AtomSet urgencyProfile,
        AtomSet interpretedContext,
        IReadOnlyList<Advertisement> available)
    {
        return available
            .Select(ad =>
            {
                var score = BeliefFunction.ScoreAdvertisement(
                    agent,
                    urgencyProfile,
                    ad.Offered,
                    ad.SourceAppearance);

                // Expected outcome: offered atoms clamped by absorption, adjusted by interpreted context
                var expectedAtoms = ad.Offered.Atoms.Select(a =>
                {
                    var absorption = agent.Absorption[a.Label];
                    var effective = absorption > 0f
                        ? MathF.Min(MathF.Abs(a.Magnitude), absorption)
                        : MathF.Abs(a.Magnitude);
                    effective = a.Magnitude < 0f ? -effective : effective;
                    var contextModifier = interpretedContext[a.Label];
                    return new Atom(a.Label, effective + contextModifier);
                });

                return new ScoredAction(
                    Advertisement: ad,
                    Score: score,
                    ExpectedOutcome: new AtomSet(expectedAtoms));
            })
            .OrderByDescending(a => a.Score)
            .ToList();
    }

    // ── Phase 2 ──────────────────────────────────────────────────────────────

    private static List<Intent> DeclareIntent(AgentTickState state)
    {
        if (state.ScoredActions.Count == 0) return [];

        var best = state.ScoredActions[0];

        return
        [
            new Intent(
                Declarer: state.Agent,
                Target: best.Advertisement.Source,
                ExpectedOutcome: best.ExpectedOutcome,
                RequiresConsent: best.Advertisement.RequiresConsent)
            {
                RawOffered = best.Advertisement.Offered
            }
        ];
    }

    // ── Phase 3 ──────────────────────────────────────────────────────────────

    private List<Resolution> Resolve(List<Intent> intents)
    {
        var resolutions = new List<Resolution>();
        var byTarget = intents.GroupBy(i => i.Target.Id);

        foreach (var group in byTarget)
        {
            var competing = group.ToList();

            if (!competing[0].RequiresConsent)
            {
                // Passive object — urgency-weighted arbitration
                if (competing.Count == 1)
                {
                    var intent = competing[0];
                    resolutions.Add(new Resolution(
                        Intent: intent,
                        Succeeded: true,
                        ActualOutcome: intent.RawOffered ?? intent.ExpectedOutcome));
                }
                else
                {
                    var ranked = competing
                        .OrderByDescending(i => i.Declarer.Urgency.Atoms.Sum(a => MathF.Abs(a.Magnitude)))
                        .ToList();

                    var winner = ranked[0];
                    foreach (var intent in competing)
                    {
                        var won = intent == winner;
                        resolutions.Add(new Resolution(
                            Intent: intent,
                            Succeeded: won,
                            ActualOutcome: won
                                ? (intent.RawOffered ?? intent.ExpectedOutcome)
                                : AtomSet.Empty,
                            FailureReason: won ? null : "Outbid by higher urgency claimant"));
                    }
                }
            }
            else
            {
                // Active agent — disposition-aware consent with capacity allocation
                var target = _agents.FirstOrDefault(a => a.Id == competing[0].Target.Id);
                if (target is null)
                {
                    foreach (var intent in competing)
                        resolutions.Add(new Resolution(
                            Intent: intent,
                            Succeeded: false,
                            ActualOutcome: AtomSet.Empty,
                            FailureReason: "Target agent not found"));
                    continue;
                }

                var consentResults = EvaluateConsentWithCapacity(target, competing);
                resolutions.AddRange(consentResults);
            }
        }

        return resolutions;
    }

    /// <summary>
    /// Evaluates consent for multiple competing proposals against an active agent target.
    /// The target's disposition toward each proposer influences score and capacity allocation.
    /// Capacity is allocated greedily by score — highest scoring proposals get priority.
    /// </summary>
    private static List<Resolution> EvaluateConsentWithCapacity(
        Agent target,
        List<Intent> proposals)
    {
        var targetUrgency = UrgencyFunction.ComputeProfile(
            target.State,
            target.Preference,
            target.Urgency,
            AtomSet.Empty);

        // Score each proposal through target's belief function.
        // Two distinct atom-sets are needed:
        // - Identity context: who is proposing — used to match belief/disposition frames
        //   Contains the proposer's id atom so disposition frames fire correctly.
        //   Falls back to appearance if populated, otherwise constructs from agent id.
        // - Offer content: what they bring — their state atoms used for raw scoring
        var scored = proposals
            .Select(intent =>
            {
                // Identity: appearance if populated (contains id atom), else build from id
                var identityContext = intent.Declarer.Appearance.Count > 0
                    ? intent.Declarer.Appearance
                    : new AtomSet(new Atom(intent.Declarer.Id, 1.0f))
                        .MergeWith(intent.Declarer.State);

                // Offer: proposer's state atoms (what they bring to the interaction)
                var offerContent = intent.Declarer.State.Count > 0
                    ? intent.Declarer.State
                    : identityContext;

                var score = BeliefFunction.ScoreAdvertisement(
                    target,
                    targetUrgency,
                    offerContent,      // what is being offered
                    identityContext);  // who is offering it — fires disposition frames
                return (intent, score);
            })
            .OrderByDescending(x => x.score)
            .ToList();

        // Target's remaining capacity per atom — from absorption collection
        var remainingCapacity = new Dictionary<string, float>(
            target.Absorption.Atoms.ToDictionary(a => a.Label, a => a.Magnitude));

        var results = new List<Resolution>();

        foreach (var (intent, score) in scored)
        {
            if (score <= 0f)
            {
                // Target's disposition refuses this proposer
                results.Add(new Resolution(
                    Intent: intent,
                    Succeeded: false,
                    ActualOutcome: AtomSet.Empty,
                    FailureReason: "Target disposition unfavorable"));
                continue;
            }

            // Allocate capacity per offered atom
            var actualAtoms = new List<Atom>();
            var anyDelivered = false;

            foreach (var offered in (intent.RawOffered ?? intent.ExpectedOutcome).Atoms)
            {
                var available = remainingCapacity.TryGetValue(offered.Label, out var cap)
                    ? cap
                    : MathF.Abs(offered.Magnitude); // no capacity limit defined — full delivery

                var deliver = MathF.Min(MathF.Abs(offered.Magnitude), available);
                if (deliver > float.Epsilon)
                {
                    var signed = offered.Magnitude < 0f ? -deliver : deliver;
                    actualAtoms.Add(new Atom(offered.Label, signed));
                    remainingCapacity[offered.Label] = available - deliver;
                    anyDelivered = true;
                }
            }

            if (anyDelivered)
            {
                var effectiveRate = actualAtoms.Count > 0 && (intent.RawOffered ?? intent.ExpectedOutcome).Count > 0
                    ? actualAtoms.Sum(a => MathF.Abs(a.Magnitude)) /
                      (intent.RawOffered ?? intent.ExpectedOutcome).Atoms.Sum(a => MathF.Abs(a.Magnitude))
                    : 0f;

                results.Add(new Resolution(
                    Intent: intent,
                    Succeeded: true,
                    ActualOutcome: new AtomSet(actualAtoms),
                    EffectiveRate: effectiveRate));
            }
            else
            {
                results.Add(new Resolution(
                    Intent: intent,
                    Succeeded: false,
                    ActualOutcome: AtomSet.Empty,
                    FailureReason: "Target capacity exhausted"));
            }
        }

        return results;
    }

    // ── Phase 4 ──────────────────────────────────────────────────────────────

    private static Agent Integrate(AgentTickState state, List<Resolution> resolutions)
    {
        var agent = state.Agent;
        var resolution = resolutions.FirstOrDefault(r => r.Intent.Declarer.Id == agent.Id);

        Agent updatedAgent;

        if (resolution is { Succeeded: true })
        {
            var memory = Memory.Form(
                frame: new Frame(state.InterpretedContext, resolution.ActualOutcome),
                expected: resolution.Intent.ExpectedOutcome,
                actual: resolution.ActualOutcome,
                urgencyProfile: state.UrgencyProfile);

            var newState = agent.State.MergeWith(resolution.ActualOutcome);
            updatedAgent = agent
                .WithUpdatedCollection(CollectionRoles.State, newState)
                .WithMemory(memory);
        }
        else if (resolution is { Succeeded: false })
        {
            var memory = Memory.Form(
                frame: new Frame(state.InterpretedContext, AtomSet.Empty),
                expected: resolution.Intent.ExpectedOutcome,
                actual: AtomSet.Empty,
                urgencyProfile: state.UrgencyProfile);

            updatedAgent = agent.WithMemory(memory);
        }
        else
        {
            updatedAgent = agent;
        }

        updatedAgent = updatedAgent.WithUpdatedCollection(
            CollectionRoles.Appearance, updatedAgent.State);

        return updatedAgent;
    }

    // ── Supporting types ──────────────────────────────────────────────────────

    private sealed record AgentTickState(
        Agent Agent,
        Observation? PerceptionTree,
        AtomSet InterpretedContext,
        AtomSet UrgencyProfile,
        List<ScoredAction> ScoredActions);

    private sealed record ScoredAction(
        Advertisement Advertisement,
        float Score,
        AtomSet ExpectedOutcome);
}