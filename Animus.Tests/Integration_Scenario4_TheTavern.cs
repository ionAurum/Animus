using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Integration tests — Scenario 4 — The Tavern
/// Tests Barkeep active consent and multi-tick social emergence.
/// </summary>
[TestClass]
public class Integration_Scenario4_TheTavern
{
    private static Agent BuildAgent(string id, float hunger, float thirst, float social) =>
        new Agent(id)
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", hunger), new Atom("thirst", thirst), new Atom("social", social))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", 1.0f), new Atom("thirst", 1.0f), new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f), new Atom("thirst", 2.0f), new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
                new AtomSet(new Atom("hunger", -0.05f), new Atom("thirst", -0.05f), new Atom("social", -0.05f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("thirst", 0.6f), new Atom("social", 0.4f), new Atom("hunger", 0.5f))));

    private static Agent BuildBarkeep() =>
        new Agent("barkeep")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.6f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
                new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f))));

    [TestMethod]
    public void Barkeep_ConsentsToServeAlice_WhenSheOffersPositiveSocial()
    {
        var alice    = BuildAgent("alice", 0.45f, 0.75f, 0.35f);
        var barkeep  = BuildBarkeep();

        var aliceAd = new Advertisement(
            Source: alice,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("alice", 1.0f), new Atom("social", 0.35f)),
            RequiresConsent: true);

        var barkeepAd = new Advertisement(
            Source: barkeep,
            Offered: new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("alice",   barkeepAd)
            .WithStaticAdvertisements("barkeep", aliceAd);

        var loop = new SimulationLoop(env, [alice, barkeep]);
        loop.Step();

        var resolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "alice");
        Assert.IsNotNull(resolution, "Alice should have a resolution");
        Assert.IsTrue(resolution.Succeeded, "Barkeep should have consented to serve Alice");
        Assert.IsTrue(resolution.EffectiveRate > 0f, "Effective rate should be positive");
    }

    [TestMethod]
    public void Barkeep_SplitsCapacity_BetweenAliceAndCarol()
    {
        var alice   = BuildAgent("alice", 0.45f, 0.75f, 0.35f);
        var carol   = BuildAgent("carol", 0.65f, 0.45f, 0.55f);
        var barkeep = BuildBarkeep();

        // Barkeep has thirst service capacity of 0.6f total
        // Alice and Carol both want thirst service at 0.5f each — exceeds capacity

        var aliceAd = new Advertisement(
            Source: alice,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("alice", 1.0f), new Atom("social", 0.35f)),
            RequiresConsent: true);

        var carolAd = new Advertisement(
            Source: carol,
            Offered: new AtomSet(new Atom("social", 0.2f)),
            SourceAppearance: new AtomSet(new Atom("carol", 1.0f), new Atom("social", 0.55f)),
            RequiresConsent: true);

        var barkeepAdForAlice = new Advertisement(
            Source: barkeep,
            Offered: new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        var barkeepAdForCarol = new Advertisement(
            Source: barkeep,
            Offered: new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("alice",   barkeepAdForAlice)
            .WithStaticAdvertisements("carol",   barkeepAdForCarol)
            .WithStaticAdvertisements("barkeep", aliceAd, carolAd);

        var loop = new SimulationLoop(env, [alice, carol, barkeep]);
        loop.Step();

        var aliceResolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "alice");
        var carolResolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "carol");

        Assert.IsNotNull(aliceResolution, "Alice should have a resolution");
        Assert.IsNotNull(carolResolution, "Carol should have a resolution");

        // At least one should have succeeded — Barkeep has some capacity
        var totalDelivered = (aliceResolution.Succeeded ? aliceResolution.ActualOutcome["thirst"] : 0f)
                           + (carolResolution.Succeeded ? carolResolution.ActualOutcome["thirst"] : 0f);

        Assert.IsTrue(totalDelivered <= 0.6f + float.Epsilon,
            $"Total thirst delivered ({totalDelivered}) should not exceed Barkeep's capacity (0.6f)");
        Assert.IsTrue(totalDelivered > 0f,
            "At least some thirst should have been delivered");
    }

    [TestMethod]
    public void Barkeep_RefusesProposer_WhenDispositionIsNegative()
    {
        var jimmy   = BuildAgent("jimmy", 0.3f, 0.5f, 0.4f);
        var barkeep = BuildBarkeep()
            // Barkeep has a negative disposition toward Jimmy
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("social", -0.9f))));

        var jimmyAd = new Advertisement(
            Source: jimmy,
            Offered: new AtomSet(new Atom("social", 0.2f)),
            SourceAppearance: new AtomSet(new Atom("jimmy", 1.0f), new Atom("social", 0.4f)),
            RequiresConsent: true);

        var barkeepAd = new Advertisement(
            Source: barkeep,
            Offered: new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("jimmy",   barkeepAd)
            .WithStaticAdvertisements("barkeep", jimmyAd);

        var loop = new SimulationLoop(env, [jimmy, barkeep]);
        loop.Step();

        var jimmyResolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "jimmy");
        Assert.IsNotNull(jimmyResolution, "Jimmy should have a resolution");
        Assert.IsFalse(jimmyResolution.Succeeded,
            "Barkeep should refuse Jimmy due to negative disposition");
    }

    [TestMethod]
    public void SocialInteractions_Emerge_BetweenAgentsWhoSharedContext()
    {
        var alice   = BuildAgent("alice",   0.45f, 0.75f, 0.35f);
        var carol   = BuildAgent("carol",   0.65f, 0.45f, 0.55f);
        var barkeep = BuildBarkeep();

        var barkeepAd = new Advertisement(
            Source: barkeep,
            Offered: new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("barkeep", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        // Tick 1: both Alice and Carol interact with Barkeep
        var env = new TestEnvironment()
            .WithStaticAdvertisements("alice",   barkeepAd)
            .WithStaticAdvertisements("carol",   barkeepAd)
            .WithStaticAdvertisements("barkeep", new Advertisement(
                Source: alice,
                Offered: new AtomSet(new Atom("social", 0.3f)),
                SourceAppearance: new AtomSet(new Atom("alice", 1.0f), new Atom("social", 0.35f)),
                RequiresConsent: true));

        var loop = new SimulationLoop(env, [alice, carol, barkeep]);
        loop.Step();

        // After tick 1, Alice and Carol have both appeared in the same context
        // Tick 2: offer social advertisements between Alice and Carol
        var aliceAfterTick1 = loop.Agents.First(a => a.Id == "alice");
        var carolAfterTick1 = loop.Agents.First(a => a.Id == "carol");

        var carolAd = new Advertisement(
            Source: carolAfterTick1,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: carolAfterTick1.Appearance,
            RequiresConsent: true);

        var aliceAd = new Advertisement(
            Source: aliceAfterTick1,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: aliceAfterTick1.Appearance,
            RequiresConsent: true);

        var env2 = new TestEnvironment()
            .WithStaticAdvertisements("alice", carolAd)
            .WithStaticAdvertisements("carol",  aliceAd);

        var loop2 = new SimulationLoop(env2, loop.Agents.ToList());
        loop2.Step();

        // At least one social interaction should have occurred between Alice and Carol
        var socialResolutions = env2.AppliedResolutions
            .Where(r => r.Succeeded && r.ActualOutcome["social"] > 0f)
            .ToList();

        Assert.IsTrue(socialResolutions.Count > 0,
            "Social interactions should emerge between agents who shared context");
    }
}
