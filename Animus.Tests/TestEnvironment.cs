using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// A simple injectable test environment for integration tests.
/// Allows explicit control over what each agent perceives and
/// what advertisements are available to them each tick.
/// </summary>
public sealed class TestEnvironment : IEnvironment
{
    private readonly Dictionary<string, Func<Agent, Observation?>> _perceptionProviders = [];
    private readonly Dictionary<string, Func<Agent, IReadOnlyList<Advertisement>>> _advertisementProviders = [];

    public List<Resolution> AppliedResolutions { get; } = [];
    public List<(IReadOnlyList<Agent> Agents, int Tick)> TickHistory { get; } = [];

    /// <summary>
    /// Sets the perception tree provider for a specific agent.
    /// </summary>
    public TestEnvironment WithPerception(string agentId, Func<Agent, Observation?> provider)
    {
        _perceptionProviders[agentId] = provider;
        return this;
    }

    /// <summary>
    /// Sets a static observation for a specific agent every tick.
    /// </summary>
    public TestEnvironment WithStaticPerception(string agentId, Observation observation)
    {
        _perceptionProviders[agentId] = _ => observation;
        return this;
    }

    /// <summary>
    /// Sets the advertisement provider for a specific agent.
    /// </summary>
    public TestEnvironment WithAdvertisements(string agentId, Func<Agent, IReadOnlyList<Advertisement>> provider)
    {
        _advertisementProviders[agentId] = provider;
        return this;
    }

    /// <summary>
    /// Sets static advertisements for a specific agent every tick.
    /// </summary>
    public TestEnvironment WithStaticAdvertisements(string agentId, params Advertisement[] advertisements)
    {
        _advertisementProviders[agentId] = _ => advertisements;
        return this;
    }

    // ── IEnvironment ─────────────────────────────────────────────────────────

    public Observation? GetPerceptionTree(Agent agent) =>
        _perceptionProviders.TryGetValue(agent.Id, out var provider)
            ? provider(agent)
            : null;

    public IReadOnlyList<Advertisement> GetAvailableAdvertisements(Agent agent) =>
        _advertisementProviders.TryGetValue(agent.Id, out var provider)
            ? provider(agent)
            : [];

    public void ApplyResolution(Resolution resolution) =>
        AppliedResolutions.Add(resolution);

    public void OnTickComplete(IReadOnlyList<Agent> agents, int tick) =>
        TickHistory.Add((agents, tick));
}
