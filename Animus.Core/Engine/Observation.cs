using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// An observation is a recursive data structure representing an objective
/// snapshot of externally visible state at a moment in time.
///
/// It is purely factual — it records what was presented to the world,
/// not what any agent thinks about it. Interpretation happens separately
/// when an agent's belief function processes the observation tree.
///
/// All three properties are recursive for full generality:
/// - Subject: who or what is acting
/// - Interaction: what is happening
/// - Object: who or what is being acted upon
///
/// Atom-sets at each node are appearance atom-sets — projections of
/// internal state that are externally visible at that moment.
/// </summary>
public sealed record Observation(
    ObservationNode Subject,
    ObservationNode Interaction,
    ObservationNode Object);

/// <summary>
/// A node in an observation tree — either a leaf (atom-set) or a nested observation.
/// </summary>
public abstract record ObservationNode
{
    /// <summary>
    /// A leaf node — a flat atom-set describing this part of the observation.
    /// </summary>
    public sealed record Leaf(AtomSet Atoms) : ObservationNode;

    /// <summary>
    /// A nested observation — a full subject/interaction/object triple.
    /// </summary>
    public sealed record Nested(Observation Observation) : ObservationNode;

    /// <summary>
    /// Convenience: extract the atom-set from this node.
    /// For nested nodes, returns the subject atom-set of the nested observation.
    /// </summary>
    public AtomSet ToAtomSet() => this switch
    {
        Leaf l => l.Atoms,
        Nested n => n.Observation.Subject.ToAtomSet(),
        _ => AtomSet.Empty
    };

    // ── Implicit conversions for ergonomic construction ──────────────────────

    public static implicit operator ObservationNode(AtomSet atoms) => new Leaf(atoms);
    public static implicit operator ObservationNode(Observation obs) => new Nested(obs);
}
