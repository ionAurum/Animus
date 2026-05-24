using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Core.Agents;

/// <summary>
/// An agent is a list of frames. Everything in the simulation is an agent —
/// people, animals, objects, environments. The distinction between intelligent
/// and inanimate is one of configuration complexity, not kind.
///
/// The engine identifies and operates on frames whose context contains
/// well-known collection role atoms. Unknown frames are ignored by the core
/// engine but remain available to sub-engines and other systems.
///
/// Agents exist on a continuum from simple (a chair) to complex (a person).
/// </summary>
public sealed class Agent
{
    private readonly List<Frame> _frames;
    private readonly List<Modifier> _modifiers;
    private readonly List<Memory> _memories;

    public string Id { get; }

    public Agent(string id)
    {
        Id = id;
        _frames = [];
        _modifiers = [];
        _memories = [];
    }

    private Agent(string id, List<Frame> frames, List<Modifier> modifiers, List<Memory> memories)
    {
        Id = id;
        _frames = frames;
        _modifiers = modifiers;
        _memories = memories;
    }

    // ── Frame-set access ────────────────────────────────────────────────────

    public IReadOnlyList<Frame> Frames => _frames;
    public IReadOnlyList<Modifier> Modifiers => _modifiers;
    public IReadOnlyList<Memory> Memories => _memories;

    // ── Well-known collection accessors ─────────────────────────────────────

    public AtomSet State      => GetCollection(CollectionRoles.State);
    public AtomSet Preference => GetCollection(CollectionRoles.Preference);
    public AtomSet Urgency    => GetCollection(CollectionRoles.Urgency);
    public AtomSet Decay      => GetCollection(CollectionRoles.Decay);
    public AtomSet Absorption => GetCollection(CollectionRoles.Absorption);
    public AtomSet Appearance => GetCollection(CollectionRoles.Appearance);
    public AtomSet Carried    => GetCollection(CollectionRoles.Carried);
    public AtomSet Owned      => GetCollection(CollectionRoles.Owned);

    private AtomSet GetCollection(string role) =>
        _frames
            .Where(f => f.Context.Contains(role))
            .Select(f => f.Value)
            .Aggregate(AtomSet.Empty, (acc, v) => acc.MergeWith(v));

    // ── Belief frames — context-matched against observed atom-sets ──────────

    /// <summary>
    /// Returns all belief frames whose context is not a well-known collection role.
    /// These are matched against observed contexts via the similarity function.
    /// </summary>
    public IEnumerable<Frame> BeliefFrames =>
        _frames.Where(f => !IsCollectionFrame(f));

    private static readonly HashSet<string> KnownRoles =
    [
        CollectionRoles.State,
        CollectionRoles.Preference,
        CollectionRoles.Urgency,
        CollectionRoles.Decay,
        CollectionRoles.Absorption,
        CollectionRoles.Appearance,
        CollectionRoles.Carried,
        CollectionRoles.Owned,
        CollectionRoles.Belief,
        CollectionRoles.Modifier,
        CollectionRoles.Memory,
        CollectionRoles.LearningRate,
    ];

    public static bool IsCollectionFrame(Frame f) =>
        f.Context.Labels.Any(l => KnownRoles.Contains(l));

    // ── Mutation — returns new Agent instances with copied collections ────────

    public Agent WithFrame(Frame frame) =>
        new(Id,
            [.._frames, frame],
            [.._modifiers],
            [.._memories]);

    public Agent WithModifier(Modifier modifier) =>
        new(Id,
            [.._frames],
            [.._modifiers, modifier],
            [.._memories]);

    public Agent WithMemory(Memory memory) =>
        new(Id,
            [.._frames],
            [.._modifiers],
            [.._memories, memory]);

    public Agent WithUpdatedCollection(string role, AtomSet value)
    {
        var newFrame = new Frame(new AtomSet(new Atom(role, 1f)), value);
        return new(Id,
            [.._frames.Where(f => !f.Context.Contains(role)), newFrame],
            [.._modifiers],
            [.._memories]);
    }

    /// <summary>
    /// Returns a new agent with modifiers replaced entirely by the given list.
    /// Used by the loop to swap decayed modifiers without accumulation.
    /// </summary>
    public Agent WithReplacedModifiers(IEnumerable<Modifier> modifiers) =>
        new(Id, [.._frames], [..modifiers], [.._memories]);

    /// <summary>
    /// Returns a new agent with memories replaced entirely by the given list.
    /// Used by the loop to swap decayed memories without accumulation.
    /// </summary>
    public Agent WithReplacedMemories(IEnumerable<Memory> memories) =>
        new(Id, [.._frames], [.._modifiers], [..memories]);

    /// <summary>
    /// Returns a new agent with belief frames replaced — collection frames preserved.
    /// Used by the loop to apply learning and decay without accumulation.
    /// </summary>
    public Agent WithReplacedBeliefFrames(IEnumerable<Frame> beliefFrames) =>
        new(Id,
            [.._frames.Where(IsCollectionFrame), ..beliefFrames],
            [.._modifiers],
            [.._memories]);

    public override string ToString() => $"Agent({Id})";
}
