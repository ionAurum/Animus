using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// The universal atom-set similarity function.
///
/// Answers: how well does candidate satisfy requirement?
///
/// Rules:
/// - Labels in both: contribute based on magnitude closeness
/// - Labels in candidate but not requirement: ignored
/// - Labels in requirement but not candidate: candidate treated as 0f
///   A missing label is equivalent to a zero-valued atom — no special handling needed.
///
/// The function is asymmetric: similarity(A, B) != similarity(B, A) in general.
/// A is always the requirement; B is always the candidate.
///
/// The function is stateless and context-free — it measures structural fit only.
/// All subjectivity (urgency, mood, relationship) is injected by the caller.
///
/// When used for advertisement scoring, the requirement is the urgency profile
/// and the contribution formula reflects urgency satisfaction:
///   contribution = urgency × offered magnitude
/// When used for context/modifier matching, the formula reflects magnitude closeness.
/// The caller selects the appropriate mode via the scoring parameter.
/// </summary>
public static class SimilarityFunction
{
    /// <summary>
    /// Context and modifier matching mode.
    /// Measures how closely candidate magnitudes match requirement magnitudes.
    /// Missing labels in candidate default to 0f.
    /// </summary>
    public static float Compute(AtomSet requirement, AtomSet candidate)
    {
        if (requirement.Count == 0)
            return 0f;

        var score = 0f;
        var count = 0;

        foreach (var label in requirement.Labels)
        {
            var required = requirement[label];
            var offered  = candidate[label]; // 0f if absent — missing = zero valued

            // Closeness contribution — positive when offered is close to required
            // Negative when offered falls far short
            var contribution = offered - MathF.Abs(required - offered);
            score += contribution;
            count++;
        }

        return count > 0 ? score / count : 0f;
    }

    /// <summary>
    /// Advertisement scoring mode.
    /// Measures how much urgency the advertisement satisfies.
    /// contribution = urgency × offered magnitude per matching label.
    /// Missing labels in candidate default to 0f — contribute nothing, penalize nothing.
    /// </summary>
    public static float Score(AtomSet urgencyProfile, AtomSet advertisement)
    {
        if (advertisement.Count == 0)
            return 0f;

        var score = 0f;

        foreach (var label in advertisement.Labels)
        {
            var urgency = urgencyProfile[label]; // 0f if no urgency on this label
            var offered = advertisement[label];

            // High urgency + high offer = strong positive contribution
            // Negative urgency (overshoot) + positive offer = negative contribution
            // Summed rather than averaged — multi-atom advertisements are not penalized
            // for offering breadth of satisfaction
            score += urgency * offered;
        }

        return score;
    }

    /// <summary>
    /// Computes similarity between a frame's context and an observed atom-set.
    /// </summary>
    public static float ComputeContextMatch(Frame frame, AtomSet observedContext) =>
        Compute(frame.Context, observedContext);
}
