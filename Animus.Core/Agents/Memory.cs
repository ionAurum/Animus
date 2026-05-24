using Animus.Core.Primitives;

namespace Animus.Core.Agents;

/// <summary>
/// A memory encodes what an agent expected, what actually happened,
/// and how significant the experience was.
///
/// Memories are not stored conclusions — they are inputs to the belief function,
/// weighted by their current recency value.
///
/// Formation:
/// - Expected: anticipated outcome from current beliefs (incorporating learned absorption limits)
/// - Actual: real state change that occurred
/// - Surprise: actual - expected, signed. Positive = pleasure/reinforcement, negative = pain
/// - Significance: intensity(actual) × urgency × |surprise| — seeds recency decay rate
/// - Valence: sign of net equilibrium effect — did state move toward or away from preference?
///
/// Per Axiom 5, memories transform rather than simply vanish.
/// High-significance memories consolidate into persistent dispositions over time.
/// </summary>
public sealed record Memory(
    Frame Frame,
    AtomSet Expected,
    AtomSet Actual,
    float Significance,
    float Recency,
    float DecayRate)
{
    /// <summary>
    /// The signed surprise delta per atom — actual minus expected.
    /// Positive is pleasure/reinforcement. Negative is pain/punishment.
    /// </summary>
    public AtomSet SurpriseDelta =>
        new(Expected.Labels.Union(Actual.Labels)
            .Select(l => new Atom(l, Actual[l] - Expected[l])));

    /// <summary>
    /// True if this memory has decayed to effective zero relevance.
    /// </summary>
    public bool IsExpired => Recency < float.Epsilon;

    /// <summary>
    /// Applies one tick of recency decay.
    /// </summary>
    public Memory ApplyDecay() =>
        this with { Recency = MathF.Max(0f, Recency - DecayRate) };

    /// <summary>
    /// Creates a memory from an interaction outcome.
    /// </summary>
    public static Memory Form(
        Frame frame,
        AtomSet expected,
        AtomSet actual,
        AtomSet urgencyProfile)
    {
        // All labels involved — union of expected and actual
        var allLabels = expected.Labels.Union(actual.Labels).ToList();

        // Surprise magnitude — how much did reality diverge from expectation?
        var surpriseMagnitude = allLabels.Sum(l => MathF.Abs(actual[l] - expected[l]));

        // Intensity — what was at stake? Use the larger of expected or actual per atom
        // so that getting nothing when expecting something registers as high intensity
        var intensity = allLabels.Sum(l => MathF.Max(MathF.Abs(expected[l]), MathF.Abs(actual[l])));

        // Urgency at time of formation — how urgently did the agent need resolution?
        // Use Max(urgency, |surprise|) per label so that large unexpected events
        // are always significant regardless of current urgency state.
        // A traumatic event is significant even when the agent felt safe beforehand.
        var urgency = allLabels.Sum(l =>
            MathF.Max(MathF.Abs(urgencyProfile[l]), MathF.Abs(actual[l] - expected[l])));

        // Significance seeds the recency decay rate — high significance decays slowly
        var significance = intensity * urgency * surpriseMagnitude;

        // Decay rate is inverse of significance — more significant = slower decay
        // Clamped to reasonable bounds
        var decayRate = significance > float.Epsilon
            ? Math.Clamp(1f / (significance * 10f), 0.001f, 1f)
            : 1f; // near-zero significance — decays immediately

        return new Memory(
            Frame: frame,
            Expected: expected,
            Actual: actual,
            Significance: significance,
            Recency: 1f,
            DecayRate: decayRate);
    }
}
