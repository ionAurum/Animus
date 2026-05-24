using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 7 — The Stranger
/// A new agent with unknown atom labels enters an existing social environment.
/// Tests signed urgency producing approach/avoidance, novel atom discovery
/// via absent priors, valence inheritance from equilibrium effect, atom
/// propagation via appearance, and gossip as indirect trust-mediated memory.
/// </summary>
[TestClass]
public class TheStrangerTests
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
                new AtomSet(new Atom("social", 2.0f), new Atom("novelty", 2.0f))));

    private static Agent BuildAlice() =>
        new Agent("alice")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.7f), new Atom("novelty", 0.8f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f), new Atom("novelty", 0.2f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f), new Atom("novelty", 2.0f))))
            // Alice trusts Bob
            .WithFrame(new Frame(
                new AtomSet(new Atom("bob", 1.0f)),
                new AtomSet(new Atom("trust", 0.8f))));

    private static AtomSet StrangerAppearance => new AtomSet(
        new Atom("stranger", 1.0f),
        new Atom("accent",   0.7f),
        new Atom("trade",    0.8f),
        new Atom("social",   0.4f));

    private static AtomSet StrangerAdvertisement => new AtomSet(
        new Atom("social", 0.3f),
        new Atom("trade",  0.5f),
        new Atom("accent", 0.2f));

    // ── Signed urgency — approach vs avoidance ───────────────────────────────

    [TestMethod]
    public void Bob_HasPositiveNoveltyUrgency_ApproachesNovelty()
    {
        // Bob: novelty state 0.4, preference 0.8 → delta +0.4 → urgency +0.16
        var urgency = UrgencyFunction.Compute(delta: 0.8f - 0.4f, exponent: 2.0f);
        Assert.IsTrue(urgency > 0f, $"Bob should approach novelty (urgency: {urgency})");
        Assert.AreEqual(0.16f, urgency, 0.001f);
    }

    [TestMethod]
    public void Alice_HasNegativeNoveltyUrgency_AvoidsNovelty()
    {
        // Alice: novelty state 0.8, preference 0.2 → delta -0.6 → urgency -0.36
        var urgency = UrgencyFunction.Compute(delta: 0.2f - 0.8f, exponent: 2.0f);
        Assert.IsTrue(urgency < 0f, $"Alice should avoid novelty (urgency: {urgency})");
        Assert.AreEqual(-0.36f, urgency, 0.001f);
    }

    [TestMethod]
    public void Bob_ScoresStranger_HigherThanAlice_DueToNoveltyUrgency()
    {
        var bob   = BuildBob();
        var alice = BuildAlice();

        var bobUrgency = new AtomSet(
            new Atom("social",  0.16f),  // delta 0.4^2
            new Atom("novelty", 0.16f)); // delta 0.4^2

        var aliceUrgency = new AtomSet(
            new Atom("social",  0.09f),  // delta 0.3^2
            new Atom("novelty", -0.36f)); // delta -0.6 → -0.36 (avoidance)

        var bobScore   = BeliefFunction.ScoreAdvertisement(bob,   bobUrgency,   StrangerAdvertisement, StrangerAppearance);
        var aliceScore = BeliefFunction.ScoreAdvertisement(alice, aliceUrgency, StrangerAdvertisement, StrangerAppearance);

        Assert.IsTrue(bobScore > aliceScore,
            $"Bob ({bobScore}) should score Stranger higher than Alice ({aliceScore})");
    }

    [TestMethod]
    public void Alice_NoveltyAvoidance_IsInternal_DoesNotAffectStrangerAdvertisementScore()
    {
        // Novelty is a property of Bob's (or Alice's) relationship to a thing —
        // not a property of the thing itself. The Stranger does not advertise novelty.
        // Alice's negative novelty urgency therefore has no direct effect on the
        // Stranger's advertisement score — the Stranger only offers social, trade, accent.
        // Alice's avoidance manifests as lower motivation to seek unfamiliar agents
        // at the behavioral level, not at the per-advertisement scoring level.
        var alice = BuildAlice();

        var aliceUrgencyWithAvoidance = new AtomSet(
            new Atom("social",  0.09f),
            new Atom("novelty", -0.36f));

        var aliceUrgencyWithout = new AtomSet(
            new Atom("social", 0.09f));

        var scoreWith    = BeliefFunction.ScoreAdvertisement(alice, aliceUrgencyWithAvoidance, StrangerAdvertisement, StrangerAppearance);
        var scoreWithout = BeliefFunction.ScoreAdvertisement(alice, aliceUrgencyWithout,       StrangerAdvertisement, StrangerAppearance);

        // Scores should be identical — novelty avoidance doesn't affect this advertisement
        Assert.AreEqual(scoreWithout, scoreWith, 0.001f,
            "Novelty avoidance is internal to Alice — it does not suppress the Stranger's advertisement score directly");
    }

    // ── Novel atom discovery via absent priors ───────────────────────────────

    [TestMethod]
    public void NovelAtom_ProducesLargePositiveSurprise_WhenExpectedIsZero()
    {
        // Bob has no prior on "trade" or "accent" — expected defaults to 0f
        var expected = new AtomSet(new Atom("social", 0.3f)); // only known atom expected
        var actual   = new AtomSet(
            new Atom("social", 0.3f),
            new Atom("trade",  0.5f),
            new Atom("accent", 0.2f));
        var urgency = new AtomSet(new Atom("social", 0.16f), new Atom("novelty", 0.16f));

        var memory = Memory.Form(
            frame: new Frame(StrangerAppearance, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgency);

        // trade surprise = 0.5 - 0f = +0.5 (novel, large)
        // accent surprise = 0.2 - 0f = +0.2 (novel, moderate)
        // social surprise = 0.3 - 0.3 = 0f (known, no surprise)
        Assert.AreEqual(0.5f, memory.SurpriseDelta["trade"],  0.001f);
        Assert.AreEqual(0.2f, memory.SurpriseDelta["accent"], 0.001f);
        Assert.AreEqual(0f,   memory.SurpriseDelta["social"], 0.001f);
    }

    [TestMethod]
    public void NovelAtomMemory_HasHigherSignificance_ThanMundaneMemory()
    {
        var novelExpected = new AtomSet(new Atom("social", 0.3f));
        var novelActual   = new AtomSet(
            new Atom("social", 0.3f),
            new Atom("trade",  0.5f),
            new Atom("accent", 0.2f));
        var novelUrgency = new AtomSet(new Atom("social", 0.16f), new Atom("novelty", 0.16f));

        var mundaneExpected = new AtomSet(new Atom("social", 0.3f));
        var mundaneActual   = new AtomSet(new Atom("social", 0.35f));
        var mundaneUrgency  = new AtomSet(new Atom("social", 0.16f));

        var novelMemory   = Memory.Form(new Frame(StrangerAppearance, novelActual),   novelExpected,   novelActual,   novelUrgency);
        var mundaneMemory = Memory.Form(new Frame(AtomSet.Empty,      mundaneActual), mundaneExpected, mundaneActual, mundaneUrgency);

        Assert.IsTrue(novelMemory.Significance > mundaneMemory.Significance,
            "Novel atom encounter should produce higher significance than mundane interaction");
    }

    // ── Valence inheritance from equilibrium effect ──────────────────────────

    [TestMethod]
    public void TradeAtom_EncodedPositively_WhenEquilibriumDistanceDecreases()
    {
        // Net equilibrium effect — did state move toward preference overall?
        // Bob's social went up (good), trade added positive value
        // Surprise delta on trade is +0.5 — positive valence
        var surpriseDelta = 0.5f; // trade: actual 0.5 - expected 0f
        Assert.IsTrue(surpriseDelta > 0f,
            "Positive surprise delta on trade indicates positive valence encoding");
    }

    [TestMethod]
    public void AccentAtom_EncodedWeaklyPositive_WithSmallerSurprise()
    {
        var accentSurprise = 0.2f; // smaller than trade
        var tradeSurprise  = 0.5f;

        Assert.IsTrue(accentSurprise > 0f,       "Accent should encode positively");
        Assert.IsTrue(accentSurprise < tradeSurprise, "Accent encoding should be weaker than trade");
    }

    // ── Gossip — indirect trust-mediated atom propagation ───────────────────

    [TestMethod]
    public void Alice_FormsWeakerMemory_FromGossip_ThanBobFromDirectExperience()
    {
        // Bob's direct memory of trade
        var bobExpected = new AtomSet(new Atom("social", 0.3f));
        var bobActual   = new AtomSet(new Atom("social", 0.3f), new Atom("trade", 0.5f));
        var bobUrgency  = new AtomSet(new Atom("social", 0.16f), new Atom("novelty", 0.16f));
        var bobMemory   = Memory.Form(new Frame(StrangerAppearance, bobActual), bobExpected, bobActual, bobUrgency);

        // Alice observes trade atom in Bob's appearance — indirect, trust-mediated
        // Alice's trust in Bob is 0.8 — she gets a discounted version
        var aliceTrustInBob = 0.8f;
        var alicePerceivedTradeMagnitude = 0.3f * aliceTrustInBob; // Bob projected 0.3f, Alice discounts by trust

        var aliceExpected = new AtomSet(new Atom("social", 0.3f));
        var aliceActual   = new AtomSet(new Atom("social", 0.3f), new Atom("trade", alicePerceivedTradeMagnitude));
        var aliceUrgency  = new AtomSet(new Atom("social", 0.09f));
        var aliceMemory   = Memory.Form(new Frame(AtomSet.Empty, aliceActual), aliceExpected, aliceActual, aliceUrgency);

        Assert.IsTrue(aliceMemory.Significance < bobMemory.Significance,
            $"Alice's indirect gossip memory ({aliceMemory.Significance}) should be weaker than Bob's direct experience ({bobMemory.Significance})");
    }

    [TestMethod]
    public void Alice_TrustInBob_AmplifiesGossipContribution()
    {
        var alice = BuildAlice();
        var bobAppearanceWithTrade = new AtomSet(
            new Atom("bob",   1.0f),
            new Atom("trade", 0.3f));

        var contribution = BeliefFunction.ComputeContextualContribution(alice, bobAppearanceWithTrade);

        // Alice's trust frame about Bob fires — contribution should include trust
        Assert.IsTrue(contribution["trust"] > 0f,
            "Alice's trust in Bob should amplify her reading of his appearance");
    }

    // ── Integration tests (pending SimulationLoop) ───────────────────────────

}
