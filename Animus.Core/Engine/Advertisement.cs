using Animus.Core.Agents;
using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// An advertisement is an agent's offer to other agents expressed as atoms.
/// All advertisement atom magnitudes represent maximum transfer rate per tick
/// from the offerer's perspective.
///
/// Whether the interaction requires consent depends on whether the source
/// is an active agent (with its own scoring function) or a passive object.
/// </summary>
public sealed record Advertisement(
    Agent Source,
    AtomSet Offered,
    AtomSet SourceAppearance,
    bool RequiresConsent);
