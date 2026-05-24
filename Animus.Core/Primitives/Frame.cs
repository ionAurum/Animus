namespace Animus.Core.Primitives;

/// <summary>
/// A pair of atom-sets: one describing what the frame is about (context),
/// one carrying the payload (value).
///
/// An optional Default float provides a fallback magnitude for any label
/// not explicitly present in the value atom-set. Null means no default —
/// absent labels return 0f as usual.
///
/// Higher-level concepts have frames — they are not frames.
/// Composition, not inheritance.
/// </summary>
public sealed record Frame(AtomSet Context, AtomSet Value, float? Default = null)
{
    /// <summary>
    /// A universal frame — applies regardless of context.
    /// </summary>
    public Frame(AtomSet value, float? @default = null) : this(AtomSet.Empty, value, @default) { }

    /// <summary>
    /// True if this frame has no context constraints — applies universally.
    /// </summary>
    public bool IsUniversal => Context.Count == 0;

    /// <summary>
    /// Returns the magnitude for the given label — explicit value if present,
    /// Default if set, otherwise 0f.
    /// </summary>
    public float GetValue(string label) =>
        Value.Contains(label) ? Value[label] : (Default ?? 0f);
}
