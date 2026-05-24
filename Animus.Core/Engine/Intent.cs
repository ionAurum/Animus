using Animus.Core.Agents;
using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// Represents an agent's declared intent for the current tick.
/// Declarations are collected in Phase 2 before any resolution occurs.
/// </summary>
public sealed record Intent(
    Agent Declarer,
    Agent Target,
    AtomSet ExpectedOutcome,
    bool RequiresConsent)
{
    /// <summary>
    /// True if the target is a passive object — resolved by urgency-weighted arbitration.
    /// False if the target is an active agent — resolved by mutual consent.
    /// </summary>
    public bool IsPassive => !RequiresConsent;

    /// <summary>
    /// The raw offered atom-set from the advertisement — what was actually delivered,
    /// independent of the agent's belief-adjusted expected outcome.
    /// Used by resolution to compute real surprise deltas.
    /// </summary>
    public AtomSet? RawOffered { get; init; }
}

/// <summary>
/// The result of an active agent's consent evaluation.
/// Carries both the decision and the effective transfer rate — how much
/// of the offered amount the target is willing and able to deliver this tick.
/// The effective rate reflects both capacity constraints and disposition toward
/// the proposer — a target who dislikes the proposer may allocate less capacity.
/// </summary>
public sealed record ConsentResult(
    bool Consented,
    float EffectiveRate,
    string? Reason = null);

/// <summary>
/// The result of Phase 3 resolution for a given intent.
/// </summary>
public sealed record Resolution(
    Intent Intent,
    bool Succeeded,
    AtomSet ActualOutcome,
    float EffectiveRate = 1f,
    string? FailureReason = null);
