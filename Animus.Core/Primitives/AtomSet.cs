namespace Animus.Core.Primitives;

/// <summary>
/// A collection of atoms indexed by label.
/// Has no intrinsic meaning — meaning is conferred by which collection
/// the atoms live in and what mechanisms operate on them.
/// Absent labels default to magnitude 0f — presence is any non-zero magnitude.
/// </summary>
public sealed record AtomSet
{
    private readonly Dictionary<string, float> _atoms;

    public static readonly AtomSet Empty = new();

    public AtomSet() => _atoms = [];

    public AtomSet(IEnumerable<Atom> atoms)
    {
        _atoms = atoms.ToDictionary(a => a.Label, a => a.Magnitude);
    }

    public AtomSet(params Atom[] atoms) : this((IEnumerable<Atom>)atoms) { }

    /// <summary>
    /// Returns the magnitude for the given label, or 0f if absent.
    /// </summary>
    public float this[string label] =>
        _atoms.TryGetValue(label, out var magnitude) ? magnitude : 0f;

    /// <summary>
    /// Returns true if the label is present with a non-zero magnitude.
    /// </summary>
    public bool Contains(string label) =>
        _atoms.TryGetValue(label, out var magnitude) && magnitude != 0f;

    /// <summary>
    /// All atoms in this set.
    /// </summary>
    public IEnumerable<Atom> Atoms =>
        _atoms.Select(kvp => new Atom(kvp.Key, kvp.Value));

    /// <summary>
    /// All labels in this set.
    /// </summary>
    public IEnumerable<string> Labels => _atoms.Keys;

    /// <summary>
    /// Number of atoms in this set.
    /// </summary>
    public int Count => _atoms.Count;

    /// <summary>
    /// Returns a new AtomSet with the given atom added or updated.
    /// </summary>
    public AtomSet With(Atom atom)
    {
        var updated = new Dictionary<string, float>(_atoms) { [atom.Label] = atom.Magnitude };
        return new AtomSet(updated.Select(kvp => new Atom(kvp.Key, kvp.Value)));
    }

    /// <summary>
    /// Returns a new AtomSet with the given atom removed.
    /// </summary>
    public AtomSet Without(string label)
    {
        var updated = new Dictionary<string, float>(_atoms);
        updated.Remove(label);
        return new AtomSet(updated.Select(kvp => new Atom(kvp.Key, kvp.Value)));
    }

    /// <summary>
    /// Returns a new AtomSet with the magnitudes of matching labels summed.
    /// </summary>
    public AtomSet MergeWith(AtomSet other)
    {
        var merged = new Dictionary<string, float>(_atoms);
        foreach (var atom in other.Atoms)
        {
            merged[atom.Label] = merged.TryGetValue(atom.Label, out var existing)
                ? existing + atom.Magnitude
                : atom.Magnitude;
        }
        return new AtomSet(merged.Select(kvp => new Atom(kvp.Key, kvp.Value)));
    }

    public override string ToString() =>
        $"{{ {string.Join(", ", _atoms.Select(kvp => $"(\"{kvp.Key}\", {kvp.Value}f)"))} }}";
}
