using Animus.Core.Primitives;

namespace Animus.Core.Agents;

/// <summary>
/// A modifier is a rule-like structure that watches the agent's atom space
/// and reshapes it. Modifiers are not atoms — they are a distinct concept.
///
/// A modifier has a frame (context describes when/to-whom it applies;
/// value describes what it does) and a decay frame (how effects erode over time).
///
/// Modifiers are stateless — all mutable state lives in atoms.
/// Decay is mandatory per Axiom 5 — nothing persists in its current form indefinitely.
///
/// Modifiers are first-class residents of the observable atom space.
/// Other modifiers can pattern-match against active modifiers and affect them.
/// </summary>
public sealed record Modifier(Frame Frame, Frame Decay)
{
    /// <summary>
    /// The current effective value atom-set of this modifier.
    /// Erodes over time via the decay frame.
    /// </summary>
    public AtomSet CurrentValue { get; init; } = Frame.Value;

    /// <summary>
    /// True if the modifier has decayed to effective zero on all value atoms.
    /// </summary>
    public bool IsExpired =>
        CurrentValue.Atoms.All(a => MathF.Abs(a.Magnitude) < float.Epsilon);

    /// <summary>
    /// Applies one tick of decay to the modifier's current value.
    /// Returns a new modifier with eroded magnitudes.
    /// Decay atoms are matched by label — each value atom decays at its
    /// corresponding decay rate atom's magnitude per tick.
    /// </summary>
    public Modifier ApplyDecay()
    {
        var decayed = CurrentValue.Atoms.Select(atom =>
        {
            var rate = Decay.Value[atom.Label];
            if (rate == 0f) return atom;

            // Decay toward zero — reduce magnitude by rate, clamp at zero crossing
            var newMagnitude = atom.Magnitude > 0f
                ? MathF.Max(0f, atom.Magnitude - rate)
                : MathF.Min(0f, atom.Magnitude + rate);

            return atom with { Magnitude = newMagnitude };
        });

        return this with { CurrentValue = new AtomSet(decayed) };
    }
}
