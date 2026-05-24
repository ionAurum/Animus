using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Animus — The Tavern with History ────────────────────────────────────────
//
// A scenario illustrating all currently implemented features:
//
//   Primitives & Collections  — Atom, AtomSet, Frame, well-known collections
//   Urgency                   — signed Gravity Axiom, approach and avoidance
//   Belief frames             — learned associations, disposition matching
//   Modifiers                 — trauma modifier affecting safety urgency
//   Memory                    — surprise, significance, valence, decay
//   Consent & capacity        — Barkeep scores proposers, splits capacity
//   Arbitration               — passive object contention resolved by urgency
//   Axiom 5 learning          — memories nudge dispositions each tick
//   Belief frame decay        — dispositions fade without reinforcement
//   Novel atom discovery      — Stranger introduces unknown labels
//   Signed urgency avoidance  — Alice avoids novelty (above preference)
//   Appearance projection     — state projected outward each tick

const int Ticks = 15;

// ── Agents ───────────────────────────────────────────────────────────────────

var bob = new Agent("bob")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.State, 1f)),
        new AtomSet(new Atom("hunger", 0.3f), new Atom("thirst", 0.4f),
                    new Atom("social", 0.8f), new Atom("safety", 0.9f),
                    new Atom("novelty", 0.1f)))) // Bob is novelty-starved
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
        new AtomSet(new Atom("hunger", 1.0f), new Atom("thirst", 1.0f),
                    new Atom("social", 1.0f), new Atom("safety", 1.0f),
                    new Atom("novelty", 0.8f)))) // Bob loves novelty
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
        new AtomSet(new Atom("hunger", 2.0f), new Atom("thirst", 2.0f),
                    new Atom("social", 2.0f), new Atom("safety", 3.0f), // safety very steep
                    new Atom("novelty", 2.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
        new AtomSet(new Atom("hunger", -0.05f), new Atom("thirst", -0.06f),
                    new Atom("social", -0.04f), new Atom("novelty", -0.05f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
        new AtomSet(new Atom("hunger", 0.3f), new Atom("thirst", 0.4f),
                    new Atom("social", 0.2f), new Atom("novelty", 0.5f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
        AtomSet.Empty, Default: 0.005f))  // belief frames decay slowly
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.LearningRate, 1f)),
        AtomSet.Empty, Default: 0.1f))
    // Bob's negative belief about Jimmy — past conflict
    .WithFrame(new Frame(
        new AtomSet(new Atom("jimmy", 1.0f)),
        new AtomSet(new Atom("safety", -0.4f), new Atom("social", -0.3f))))
    // Bob's positive belief about Alice
    .WithFrame(new Frame(
        new AtomSet(new Atom("alice", 1.0f)),
        new AtomSet(new Atom("social", 0.5f), new Atom("trust", 0.8f))))
    // Trauma modifier — Jimmy's past violence makes Bob hyper-vigilant about safety
    .WithModifier(new Modifier(
        Frame: new Frame(
            new AtomSet(new Atom("jimmy", 1.0f)),
            new AtomSet(new Atom("safety", 0.8f))), // raises safety urgency exponent
        Decay: new Frame(
            new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
            new AtomSet(new Atom("safety", 0.02f))))); // very slow decay — trauma lingers

var alice = new Agent("alice")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.State, 1f)),
        new AtomSet(new Atom("hunger", 0.5f), new Atom("thirst", 0.5f),
                    new Atom("social", 0.3f), new Atom("novelty", 0.9f)))) // already saturated on novelty
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
        new AtomSet(new Atom("hunger", 1.0f), new Atom("thirst", 1.0f),
                    new Atom("social", 1.0f), new Atom("novelty", 0.2f)))) // dislikes novelty
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
        new AtomSet(new Atom("hunger", 2.0f), new Atom("thirst", 2.0f),
                    new Atom("social", 2.0f), new Atom("novelty", 2.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
        new AtomSet(new Atom("hunger", -0.04f), new Atom("thirst", -0.08f),
                    new Atom("social", -0.05f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
        new AtomSet(new Atom("hunger", 0.3f), new Atom("thirst", 0.3f), new Atom("social", 0.2f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
        AtomSet.Empty, Default: 0.005f))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.LearningRate, 1f)),
        AtomSet.Empty, Default: 0.08f))
    // Alice trusts Bob
    .WithFrame(new Frame(
        new AtomSet(new Atom("bob", 1.0f)),
        new AtomSet(new Atom("social", 0.4f), new Atom("trust", 0.8f))));

var jimmy = new Agent("jimmy")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.State, 1f)),
        new AtomSet(new Atom("hunger", 0.2f), new Atom("thirst", 0.4f), new Atom("social", 0.4f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
        new AtomSet(new Atom("hunger", 1.0f), new Atom("thirst", 1.0f), new Atom("social", 1.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
        new AtomSet(new Atom("hunger", 2.0f), new Atom("thirst", 2.0f), new Atom("social", 2.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
        new AtomSet(new Atom("hunger", -0.05f), new Atom("thirst", -0.08f), new Atom("social", -0.05f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
        new AtomSet(new Atom("hunger", 0.3f), new Atom("thirst", 0.3f), new Atom("social", 0.2f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
        AtomSet.Empty, Default: 0.005f))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.LearningRate, 1f)),
        AtomSet.Empty, Default: 0.08f));

// The Stranger — arrives with novel atoms "spice" and "tales"
var stranger = new Agent("stranger")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
        new AtomSet(new Atom("stranger", 1.0f), new Atom("spice", 0.7f), new Atom("tales", 0.8f))));

// The Barkeep — active agent with capacity and disposition
var barkeep = new Agent("barkeep")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.State, 1f)),
        new AtomSet(new Atom("social", 0.5f), new Atom("purpose", 0.8f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
        new AtomSet(new Atom("social", 1.0f), new Atom("purpose", 1.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
        new AtomSet(new Atom("social", 2.0f), new Atom("purpose", 2.0f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
        new AtomSet(new Atom("social", -0.15f), new Atom("purpose", -0.02f)))) // social fades between interactions
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
        new AtomSet(new Atom("thirst", 0.5f), new Atom("hunger", 0.4f), new Atom("social", 0.6f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
        new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.5f))))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
        AtomSet.Empty, Default: 0.005f))
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.LearningRate, 1f)),
        AtomSet.Empty, Default: 0.05f))
    // Barkeep likes Alice, is neutral on Bob, dislikes Jimmy
    .WithFrame(new Frame(
        new AtomSet(new Atom("alice", 1.0f)),
        new AtomSet(new Atom("social", 0.6f))))
    .WithFrame(new Frame(
        new AtomSet(new Atom("jimmy", 1.0f)),
        new AtomSet(new Atom("social", -0.5f))));

// Passive food platter — contested resource
var foodPlatter = new Agent("food_platter")
    .WithFrame(new Frame(new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
        new AtomSet(new Atom("food_platter", 1.0f), new Atom("food", 1.0f))));

// ── Advertisements ────────────────────────────────────────────────────────────

var barkeepDrinkAd = new Advertisement(
    Source: barkeep,
    Offered: new AtomSet(new Atom("thirst", 0.4f), new Atom("social", 0.2f)),
    SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.5f)),
    RequiresConsent: true);

var foodPlatterAd = new Advertisement(
    Source: foodPlatter,
    Offered: new AtomSet(new Atom("hunger", 0.3f)),
    SourceAppearance: new AtomSet(new Atom("food_platter", 1.0f)),
    RequiresConsent: false);

var strangerAd = new Advertisement(
    Source: stranger,
    Offered: new AtomSet(new Atom("social", 0.3f), new Atom("novelty", 0.6f),
                         new Atom("tales", 0.5f), new Atom("spice", 0.3f)),
    SourceAppearance: new AtomSet(new Atom("stranger", 1.0f), new Atom("spice", 0.7f), new Atom("tales", 0.8f)),
    RequiresConsent: false); // Stranger is open to all — passive interaction

// Proposer ads — what each agent offers the barkeep
var bobProposesAd = new Advertisement(Source: bob, Offered: new AtomSet(new Atom("social", 0.3f)), SourceAppearance: new AtomSet(new Atom("bob", 1.0f)), RequiresConsent: true);
var aliceProposesAd = new Advertisement(Source: alice, Offered: new AtomSet(new Atom("social", 0.3f)), SourceAppearance: new AtomSet(new Atom("alice", 1.0f)), RequiresConsent: true);
var jimmyProposesAd = new Advertisement(Source: jimmy, Offered: new AtomSet(new Atom("social", 0.2f)), SourceAppearance: new AtomSet(new Atom("jimmy", 1.0f)), RequiresConsent: true);

// ── Environment ───────────────────────────────────────────────────────────────

var env = new TavernEnvironment(
    barkeepDrinkAd, foodPlatterAd, strangerAd,
    bobProposesAd, aliceProposesAd, jimmyProposesAd);

var loop = new SimulationLoop(env,
    [bob, alice, jimmy, barkeep, stranger, foodPlatter]);

// ── Run ───────────────────────────────────────────────────────────────────────

PrintHeader();

for (int tick = 0; tick <= Ticks; tick++)
{
    if (tick > 0)
    {
        env.ClearLog();
        loop.Step();
    }

    PrintTick(loop, tick, env);
}

PrintFinalSummary(loop);

// ── Output Helpers ────────────────────────────────────────────────────────────

static void PrintHeader()
{
    Console.WriteLine("=== Animus — The Tavern with History ===");
    Console.WriteLine();
    Console.WriteLine("Agents:");
    Console.WriteLine("  Bob     — curious, loves novelty, wary of Jimmy (trauma modifier active)");
    Console.WriteLine("  Alice   — conservative, dislikes novelty, trusts Bob");
    Console.WriteLine("  Jimmy   — neutral, dislikes by Barkeep");
    Console.WriteLine("  Barkeep — active agent with capacity 0.5 thirst/tick, likes Alice, dislikes Jimmy");
    Console.WriteLine("  Stranger — novel agent with unknown atoms: spice, tales");
    Console.WriteLine("  Food Platter — passive object, contested resource");
    Console.WriteLine();
}

static void PrintTick(SimulationLoop loop, int tick, TavernEnvironment env)
{
    Console.WriteLine($"── Tick {tick} {'─',50}");

    foreach (var agentId in new[] { "bob", "alice", "jimmy", "barkeep" })
    {
        var agent = loop.Agents.FirstOrDefault(a => a.Id == agentId);
        if (agent is null) continue;

        var stateStr = string.Join("  ", agent.State.Atoms
            .Where(a => MathF.Abs(a.Magnitude) > 0.001f)
            .Select(a => $"{a.Label}:{a.Magnitude:F2}"));

        Console.WriteLine($"  {agentId,-10} state=[{stateStr}]  memories={agent.Memories.Count}  beliefs={agent.BeliefFrames.Count()}");
    }

    if (env.LastResolutions.Count > 0)
    {
        Console.WriteLine("  Resolutions:");
        foreach (var r in env.LastResolutions)
        {
            var outcome = r.Succeeded
                ? $"✓ delivered [{string.Join(", ", r.ActualOutcome.Atoms.Select(a => $"{a.Label}:{a.Magnitude:F2}"))}] rate={r.EffectiveRate:F2}"
                : $"✗ {r.FailureReason}";
            Console.WriteLine($"    {r.Intent.Declarer.Id,-10} → {r.Intent.Target.Id,-12} {outcome}");
        }
    }

    Console.WriteLine();
}

static void PrintFinalSummary(SimulationLoop loop)
{
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("=== Final Summary ===");
    Console.WriteLine();

    foreach (var agentId in new[] { "bob", "alice", "jimmy", "barkeep" })
    {
        var agent = loop.Agents.FirstOrDefault(a => a.Id == agentId);
        if (agent is null) continue;

        Console.WriteLine($"  {agentId.ToUpper()}");
        Console.WriteLine($"    State:   {string.Join("  ", agent.State.Atoms.Where(a => MathF.Abs(a.Magnitude) > 0.001f).Select(a => $"{a.Label}:{a.Magnitude:F3}"))}");
        Console.WriteLine($"    Memories: {agent.Memories.Count}");

        var beliefs = agent.BeliefFrames.ToList();
        if (beliefs.Count > 0)
        {
            Console.WriteLine($"    Learned beliefs:");
            foreach (var frame in beliefs.Where(f => f.Value.Atoms.Any(a => MathF.Abs(a.Magnitude) > 0.001f)))
            {
                var ctx = string.Join(", ", frame.Context.Atoms.Select(a => $"{a.Label}:{a.Magnitude:F2}"));
                var val = string.Join(", ", frame.Value.Atoms.Where(a => MathF.Abs(a.Magnitude) > 0.001f).Select(a => $"{a.Label}:{a.Magnitude:F4}"));
                if (!string.IsNullOrEmpty(val))
                    Console.WriteLine($"      [{ctx}] → [{val}]");
            }
        }

        var modifiers = agent.Modifiers.ToList();
        if (modifiers.Count > 0)
        {
            Console.WriteLine($"    Active modifiers: {modifiers.Count}");
            foreach (var mod in modifiers)
            {
                var ctx = string.Join(", ", mod.Frame.Context.Atoms.Select(a => $"{a.Label}:{a.Magnitude:F2}"));
                var val = string.Join(", ", mod.CurrentValue.Atoms.Select(a => $"{a.Label}:{a.Magnitude:F3}"));
                Console.WriteLine($"      [{ctx}] → [{val}]");
            }
        }

        Console.WriteLine();
    }
}

// ── Environment ───────────────────────────────────────────────────────────────

sealed class TavernEnvironment(
    Advertisement barkeepDrinkAd,
    Advertisement foodPlatterAd,
    Advertisement strangerAd,
    Advertisement bobProposesAd,
    Advertisement aliceProposesAd,
    Advertisement jimmyProposesAd) : IEnvironment
{
    public List<Resolution> LastResolutions { get; } = [];

    public void ClearLog() => LastResolutions.Clear();

    public Observation? GetPerceptionTree(Agent agent) => null;

    public IReadOnlyList<Advertisement> GetAvailableAdvertisements(Agent agent) =>
        agent.Id switch
        {
            "bob" => [barkeepDrinkAd, foodPlatterAd, strangerAd],
            "alice" => [barkeepDrinkAd, foodPlatterAd, strangerAd],
            "jimmy" => [barkeepDrinkAd, foodPlatterAd],
            "barkeep" => [bobProposesAd, aliceProposesAd, jimmyProposesAd],
            _ => []
        };

    public void ApplyResolution(Resolution resolution) =>
        LastResolutions.Add(resolution);

    public void OnTickComplete(IReadOnlyList<Agent> agents, int tick) { }
}