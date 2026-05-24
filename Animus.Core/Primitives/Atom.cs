namespace Animus.Core.Primitives;

/// <summary>
/// The foundational primitive of the simulation engine.
/// An atom is a semantic label paired with a float magnitude.
/// Atoms are ruthlessly dumb — they carry no self-knowledge of their role.
/// Meaning is conferred entirely by which collection they live in and what operates on them.
/// </summary>
public readonly record struct Atom(string Label, float Magnitude = 0f);
