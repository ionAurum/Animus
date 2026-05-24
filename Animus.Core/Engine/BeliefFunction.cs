using Animus.Core.Agents;
using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// The belief function derives an agent's interpreted evaluation context
/// from its frame-set — the agent's working model of the world at this moment.
///
/// Beliefs are not stored directly — they are derived. The belief function
/// ingests memories weighted by recency, dispositions, and current state
/// to produce the subjective lens through which the agent interprets observations.
///
/// Inputs:
/// - Inherent: preference atom-set + f(dispositions) — present before any experience
/// - Learned: accumulated memory frames weighted by recency, active modifiers
///
/// The belief function is blind to provenance — an inherited prejudice and
/// a hard-won lesson are structurally identical inputs.
/// </summary>
public static class BeliefFunction
{
    /// <summary>
    /// Given an observed context atom-set and an agent's frame-set,
    /// returns the contextual contribution to advertisement scoring —
    /// the aggregate value atom-set derived from all matching belief frames,
    /// weighted by similarity score.
    ///
    /// Frame.Default is respected — absent labels in the frame's value atom-set
    /// contribute the default magnitude scaled by similarity.
    /// </summary>
    public static AtomSet ComputeContextualContribution(
        Agent agent,
        AtomSet observedContext)
    {
        var contribution = AtomSet.Empty;

        // Match belief frames against observed context
        foreach (var frame in agent.BeliefFrames)
        {
            if (frame.IsUniversal)
            {
                // Universal frame — contribute explicit values plus default for observed labels
                var universalContrib = new AtomSet(
                    observedContext.Labels
                        .Select(l => new Atom(l, frame.GetValue(l)))
                        .Where(a => a.Magnitude != 0f));
                contribution = contribution.MergeWith(universalContrib);
                continue;
            }

            var similarity = SimilarityFunction.ComputeContextMatch(frame, observedContext);
            if (similarity <= 0f) continue;

            // Scale explicit atoms by similarity
            var explicitScaled = frame.Value.Atoms
                .Select(a => a with { Magnitude = a.Magnitude * similarity });

            // Apply default to observed labels not explicitly in frame value
            var defaultScaled = frame.Default.HasValue
                ? observedContext.Labels
                    .Where(l => !frame.Value.Contains(l))
                    .Select(l => new Atom(l, frame.Default.Value * similarity))
                    .Where(a => a.Magnitude != 0f)
                : Enumerable.Empty<Atom>();

            var scaled = new AtomSet([..explicitScaled, ..defaultScaled]);
            contribution = contribution.MergeWith(scaled);
        }

        // Apply active modifier contributions that match the observed context
        foreach (var modifier in agent.Modifiers.Where(m => !m.IsExpired))
        {
            var similarity = SimilarityFunction.ComputeContextMatch(
                modifier.Frame, observedContext);
            if (similarity <= 0f) continue;

            var scaled = new AtomSet(
                modifier.CurrentValue.Atoms
                    .Select(a => a with { Magnitude = a.Magnitude * similarity }));
            contribution = contribution.MergeWith(scaled);
        }

        // Apply memory contributions weighted by recency and context match
        foreach (var memory in agent.Memories.Where(m => !m.IsExpired))
        {
            var similarity = SimilarityFunction.ComputeContextMatch(
                memory.Frame, observedContext);
            if (similarity <= 0f) continue;

            var weighted = new AtomSet(
                memory.Frame.Value.Atoms
                    .Select(a => a with { Magnitude = a.Magnitude * similarity * memory.Recency }));
            contribution = contribution.MergeWith(weighted);
        }

        return contribution;
    }

    /// <summary>
    /// Scores an advertisement against an agent's urgency profile,
    /// applying contextual belief contributions from the advertiser's appearance.
    /// </summary>
    public static float ScoreAdvertisement(
        Agent agent,
        AtomSet urgencyProfile,
        AtomSet advertisement,
        AtomSet advertiserAppearance)
    {
        // Raw score — how much urgency does this advertisement satisfy?
        var rawScore = SimilarityFunction.Score(urgencyProfile, advertisement);

        // Contextual contribution from belief frames matched against advertiser appearance
        var contextualContribution = ComputeContextualContribution(agent, advertiserAppearance);

        // Merge contextual contribution into advertisement and re-score
        var enrichedAdvertisement = advertisement.MergeWith(contextualContribution);
        var enrichedScore = SimilarityFunction.Score(urgencyProfile, enrichedAdvertisement);

        return enrichedScore;
    }
}
