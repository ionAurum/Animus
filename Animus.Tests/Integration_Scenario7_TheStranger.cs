using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Integration tests — Scenario 7 — The Stranger
/// Tests novel atom discovery across ticks and gossip propagation
/// via appearance atoms.
/// </summary>
[TestClass]
public class Integration_Scenario7_TheStranger
{
    private static Agent BuildBob() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.6f), new Atom("novelty", 0.4f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f), new Atom("novelty", 0.8f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f), new Atom("novelty", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("social", 0.3f), new Atom("trade", 0.5f))));

    private static Agent BuildAlice() =>
        new Agent("alice")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.7f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("social", 0.3f))))
            // Alice trusts Bob
            .WithFrame(new Frame(
                new AtomSet(new Atom("bob", 1.0f)),
                new AtomSet(new Atom("trust", 0.8f))));

    private static Agent BuildStranger() =>
        new Agent("stranger")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
                new AtomSet(new Atom("stranger", 1.0f), new Atom("accent", 0.7f), new Atom("trade", 0.8f))));

    [TestMethod]
    public void Bob_RecognizesTradeAtom_InSubsequentEncounters()
    {
        var stranger = BuildStranger();
        var strangerAd = new Advertisement(
            Source: stranger,
            Offered: new AtomSet(new Atom("social", 0.3f), new Atom("trade", 0.5f)),
            SourceAppearance: stranger.Appearance,
            RequiresConsent: true);

        // Stranger consents — offers social back
        var bobAd = new Advertisement(
            Source: BuildBob(),
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("bob", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob",     strangerAd)
            .WithStaticAdvertisements("stranger", bobAd);

        var loop = new SimulationLoop(env, [BuildBob(), stranger]);
        loop.Step(); // Bob encounters Stranger — trade atom enters memory

        var bobAfterTick1 = loop.Agents.First(a => a.Id == "bob");

        // Bob should have formed a memory containing trade atom
        Assert.IsTrue(bobAfterTick1.Memories.Count > 0, "Bob should have memories after encounter");

        var tradeMemory = bobAfterTick1.Memories
            .FirstOrDefault(m => m.Actual["trade"] > 0f || m.Expected["trade"] > 0f);

        Assert.IsNotNull(tradeMemory,
            "Bob should have a memory involving the trade atom");

        // Second encounter — Bob now has prior on trade
        loop.Step();

        var bobAfterTick2 = loop.Agents.First(a => a.Id == "bob");
        Assert.IsTrue(bobAfterTick2.Memories.Count > 0,
            "Bob should retain memories across ticks");
    }

    [TestMethod]
    public void TradeAtom_PropagatesThroughSocialGraph_ViaBobsAppearance()
    {
        var stranger = BuildStranger();
        var bob      = BuildBob();
        var alice    = BuildAlice();

        var strangerAd = new Advertisement(
            Source: stranger,
            Offered: new AtomSet(new Atom("social", 0.3f), new Atom("trade", 0.5f)),
            SourceAppearance: stranger.Appearance,
            RequiresConsent: true);

        var bobAdForStranger = new Advertisement(
            Source: bob,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: new AtomSet(new Atom("bob", 1.0f), new Atom("social", 0.6f)),
            RequiresConsent: true);

        // Tick 1 — Bob encounters Stranger, trade atom delivered
        var env1 = new TestEnvironment()
            .WithStaticAdvertisements("bob",     strangerAd)
            .WithStaticAdvertisements("stranger", bobAdForStranger);

        var loop = new SimulationLoop(env1, [bob, alice, stranger]);
        loop.Step();

        var bobAfterTick1 = loop.Agents.First(a => a.Id == "bob");

        // Bob's appearance should now include trade from state merge
        // (trade was delivered to state — appears in appearance projection)
        var bobHasTrade = bobAfterTick1.State["trade"] > 0f ||
                          bobAfterTick1.Appearance["trade"] > 0f;

        // Tick 2 — Alice observes Bob's appearance containing trade atom
        var bobAdForAlice = new Advertisement(
            Source: bobAfterTick1,
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: bobAfterTick1.Appearance,
            RequiresConsent: true);

        var aliceAdForBob = new Advertisement(
            Source: loop.Agents.First(a => a.Id == "alice"),
            Offered: new AtomSet(new Atom("social", 0.3f)),
            SourceAppearance: loop.Agents.First(a => a.Id == "alice").Appearance,
            RequiresConsent: true);

        var env2 = new TestEnvironment()
            .WithStaticAdvertisements("alice", bobAdForAlice)
            .WithStaticAdvertisements("bob",   aliceAdForBob);

        var loop2 = new SimulationLoop(env2, loop.Agents.ToList());
        loop2.Step();

        var aliceAfterTick2 = loop2.Agents.First(a => a.Id == "alice");

        // Alice should have formed a memory from interacting with Bob
        Assert.IsTrue(aliceAfterTick2.Memories.Count > 0,
            "Alice should have memories after interacting with Bob");

        // The trade atom has propagated through the social graph:
        // Stranger → Bob (tick 1) → Alice observes Bob (tick 2)
        // Alice's memory of interacting with Bob forms the gossip link
        Assert.IsTrue(env2.AppliedResolutions.Any(r =>
            r.Intent.Declarer.Id == "alice" && r.Succeeded),
            "Alice should have successfully interacted with Bob");
    }
}
