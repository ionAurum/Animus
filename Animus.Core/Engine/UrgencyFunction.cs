using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// Implements the Gravity Axiom — the universal urgency attractor function.
///
/// urgency(atom) = sign(delta) × |delta| ^ (baseExponent + Σ urgencyModifiers)
///
/// The sign of the delta is preserved to distinguish approach from avoidance:
/// - Positive urgency: agent is below preference, drives approach toward satisfiers
/// - Negative urgency: agent is above preference, drives avoidance of further input
///
/// The exponent shape defines perceived personality on that axis:
/// - exponent = 1:   linear response
/// - exponent > 1:   convex — small deltas feel small, large feel catastrophic (anxious)
/// - 0 < exponent < 1: concave — even moderate deltas feel urgent (hair-trigger)
/// - exponent → 0:  flat — indifference (stoic)
/// - exponent < 0:  inverted — larger deltas feel less urgent (pathological)
/// </summary>
public static class UrgencyFunction
{
    /// <summary>
    /// Computes signed urgency for a single atom.
    /// </summary>
    /// <param name="delta">preference - state for this atom</param>
    /// <param name="exponent">base urgency exponent + sum of urgency modifiers</param>
    public static float Compute(float delta, float exponent)
    {
        if (delta == 0f) return 0f;

        var sign = MathF.Sign(delta);
        var magnitude = MathF.Abs(delta);

        // Guard against invalid exponent operations
        var urgencyMagnitude = exponent switch
        {
            0f => 1f,                           // flat — always urgency 1 regardless of delta
            _ => MathF.Pow(magnitude, exponent) // standard power curve
        };

        return sign * urgencyMagnitude;
    }

    /// <summary>
    /// Computes the full urgency profile for an agent given its state,
    /// preference, urgency exponent, and active urgency modifier collections.
    /// </summary>
    public static AtomSet ComputeProfile(
        AtomSet state,
        AtomSet preference,
        AtomSet urgencyExponents,
        AtomSet urgencyModifierSums)
    {
        var results = new List<Atom>();

        // Compute urgency for every label that appears in preference
        foreach (var label in preference.Labels)
        {
            var currentState = state[label];
            var pref = preference[label];
            var delta = pref - currentState;

            var baseExponent = urgencyExponents[label];
            if (baseExponent == 0f) baseExponent = 1f; // default linear if not specified

            var modifierSum = urgencyModifierSums[label];
            var exponent = baseExponent + modifierSum;

            var urgency = Compute(delta, exponent);
            results.Add(new Atom(label, urgency));
        }

        return new AtomSet(results);
    }
}
