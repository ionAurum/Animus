namespace Animus.Core.Engine;

/// <summary>
/// The engine's well-known collection role labels.
/// Frames whose context contains one of these atoms are interpreted
/// by the core engine accordingly.
///
/// Unknown context atoms are ignored cleanly — the engine never breaks
/// on unrecognized frames. Unknown frames may be meaningful to sub-engines,
/// the environment layer, or future extensions.
///
/// The float magnitude on collection role atoms is non-zero = present
/// in the core engine. Sub-engines may use the float for blending semantics.
/// </summary>
public static class CollectionRoles
{
    public const string State        = "state";
    public const string Preference   = "preference";
    public const string Urgency      = "urgency";
    public const string Decay        = "decay";
    public const string Absorption   = "absorption";
    public const string Appearance   = "appearance";
    public const string Carried      = "carried";
    public const string Owned        = "owned";
    public const string Belief       = "belief";
    public const string Modifier     = "modifier";
    public const string Memory       = "memory";
    public const string LearningRate = "learning_rate";
}
