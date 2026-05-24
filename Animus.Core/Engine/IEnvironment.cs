using Animus.Core.Agents;
using Animus.Core.Primitives;

namespace Animus.Core.Engine;

/// <summary>
/// The environment interface — the boundary between the core engine and the
/// world layer. The environment has a god's-eye view of the simulation and
/// is responsible for:
///
/// - Projecting each agent's perception tree from its own point of view,
///   accounting for spatial proximity, signal attenuation, and obstacles
/// - Providing each agent's available advertisements — what is reachable
///   and visible to this agent this tick
/// - Receiving and applying world state changes after resolution
///
/// The core engine has no knowledge of space, physics, or world structure.
/// It only knows what the environment tells it.
/// </summary>
public interface IEnvironment
{
    /// <summary>
    /// Returns the perception tree for the given agent this tick.
    /// The environment is responsible for attenuation, occlusion, and
    /// any other spatial or contextual filtering.
    /// Returns null if the agent perceives nothing.
    /// </summary>
    Observation? GetPerceptionTree(Agent agent);

    /// <summary>
    /// Returns all advertisements available to the given agent this tick.
    /// The environment determines what is reachable, visible, and accessible.
    /// </summary>
    IReadOnlyList<Advertisement> GetAvailableAdvertisements(Agent agent);

    /// <summary>
    /// Called after resolution — applies actual state changes to the world.
    /// The environment may update spatial state, reservations, inventory,
    /// and any other world-level bookkeeping.
    /// </summary>
    void ApplyResolution(Resolution resolution);

    /// <summary>
    /// Called at the end of each tick after all agents have updated their
    /// appearance. Allows the environment to update its world model.
    /// </summary>
    void OnTickComplete(IReadOnlyList<Agent> agents, int tick);
}
