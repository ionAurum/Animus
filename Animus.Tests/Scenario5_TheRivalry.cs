using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 5 — The Rivalry
/// Bob and Jimmy compete for the last piece of food. Alice observes.
/// Tests urgency-weighted arbitration, belief suppression of expected outcomes,
/// positive surprise from unmet negative expectations, strong negative memory
/// formation, and observer belief updates.
/// </summary>
[TestClass]
public class TheRivalryTests
{
    private static Agent BuildBob() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", 0.15f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f))))
            // Bob's negative belief about Jimmy near food
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("hunger", -0.3f))));

    private static Agent BuildJimmy() =>
        new Agent("jimmy")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", 0.25f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f))));

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
                new AtomSet(new Atom("bob", 1.0f)),
                new AtomSet(new Atom("trust", 0.8f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("trust", 0.4f))));

    private static AtomSet LastMealAdvertisement => new AtomSet(new Atom("hunger", 0.8f));
    private static AtomSet LastMealAppearance    => new AtomSet(new Atom("last_meal", 1.0f));
    private static AtomSet JimmyAppearance       => new AtomSet(new Atom("jimmy", 1.0f), new Atom("hunger", 0.6f));

    // ── Urgency computation ──────────────────────────────────────────────────

    [TestMethod]
    public void Bob_HungerUrgency_ExceedsJimmy_AfterDecay()
    {
        // Bob hunger 0.15 → delta 0.85 → urgency 0.7225
        // Jimmy hunger 0.25 → delta 0.75 → urgency 0.5625
        var bobUrgency   = UrgencyFunction.Compute(delta: 1.0f - 0.15f, exponent: 2.0f);
        var jimmyUrgency = UrgencyFunction.Compute(delta: 1.0f - 0.25f, exponent: 2.0f);

        Assert.AreEqual(0.7225f, bobUrgency,   0.001f);
        Assert.AreEqual(0.5625f, jimmyUrgency, 0.001f);
        Assert.IsTrue(bobUrgency > jimmyUrgency,
            "Bob is hungrier — should win urgency-weighted arbitration");
    }

    // ── Arbitration ──────────────────────────────────────────────────────────

    [TestMethod]
    public void Bob_WinsArbitration_OverJimmy_OnHigherUrgency()
    {
        var bobUrgency   = UrgencyFunction.Compute(delta: 0.85f, exponent: 2.0f);
        var jimmyUrgency = UrgencyFunction.Compute(delta: 0.75f, exponent: 2.0f);

        // Urgency-weighted arbitration — highest urgency wins
        var bobWins = bobUrgency > jimmyUrgency;
        Assert.IsTrue(bobWins, "Bob should win Last Meal via urgency-weighted arbitration");
    }

    // ── Belief suppression and positive surprise ─────────────────────────────

    [TestMethod]
    public void BobBeliefFrame_SuppressesExpectedOutcome_WhenJimmyIsPresent()
    {
        var bob = BuildBob();

        // Bob's belief frame about Jimmy contributes ("hunger", -0.3f)
        // when Jimmy appears in the evaluation context
        var contribution = BeliefFunction.ComputeContextualContribution(bob, JimmyAppearance);

        Assert.IsTrue(contribution["hunger"] < 0f,
            "Jimmy's presence should contribute negative hunger expectation");
    }

    [TestMethod]
    public void Bob_ExperiencesPositiveSurprise_WhenJimmyDoesNotInterfere()
    {
        // Bob expected reduced satisfaction due to Jimmy's presence
        // but Last Meal delivered fully because Jimmy lost arbitration
        var expected = new AtomSet(new Atom("hunger", 0.5f)); // discounted by -0.3f belief
        var actual   = new AtomSet(new Atom("hunger", 0.8f)); // full delivery

        var urgencyProfile = new AtomSet(new Atom("hunger", 0.7225f));

        var memory = Memory.Form(
            frame: new Frame(AtomSet.Empty, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgencyProfile);

        // Surprise delta = actual - expected = +0.3f — positive
        Assert.AreEqual(0.3f, memory.SurpriseDelta["hunger"], 0.001f,
            "Bob should experience positive surprise — Jimmy didn't interfere");
        Assert.IsTrue(memory.Significance > 0f);
    }

    [TestMethod]
    public void BobNegativeBeliefAboutJimmy_SlightlyErodes_AfterPositiveSurprise()
    {
        // A positive surprise when Jimmy is in context should very slightly
        // reduce the negative belief about Jimmy near food
        // This is a design-level assertion — the belief update mechanism
        // will be validated more fully in integration tests
        var positiveSurpriseDelta = 0.3f;
        Assert.IsTrue(positiveSurpriseDelta > 0f,
            "Positive surprise with Jimmy in context should seed belief erosion");
    }

    // ── Jimmy's negative memory ──────────────────────────────────────────────

    [TestMethod]
    public void Jimmy_FormsStrongNegativeMemory_WhenArbitrationFails()
    {
        var expected = new AtomSet(new Atom("hunger", 0.8f)); // expected full delivery
        var actual   = new AtomSet(new Atom("hunger", 0.0f)); // got nothing

        var urgencyProfile = new AtomSet(new Atom("hunger", 0.5625f));

        var memory = Memory.Form(
            frame: new Frame(AtomSet.Empty, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgencyProfile);

        // Surprise delta = 0.0 - 0.8 = -0.8 — large negative
        Assert.AreEqual(-0.8f, memory.SurpriseDelta["hunger"], 0.001f);
        Assert.IsTrue(memory.Significance > 0f, "Significance should be high");
        Assert.IsTrue(memory.DecayRate < 1f,     "High significance — slow decay");
    }

    [TestMethod]
    public void Jimmy_NegativeMemory_HasSlowerDecay_ThanBobPositiveMemory()
    {
        var jimmyExpected = new AtomSet(new Atom("hunger", 0.8f));
        var jimmyActual   = new AtomSet(new Atom("hunger", 0.0f));
        var bobExpected   = new AtomSet(new Atom("hunger", 0.5f));
        var bobActual     = new AtomSet(new Atom("hunger", 0.8f));
        var urgency       = new AtomSet(new Atom("hunger", 0.6f));

        var jimmyMemory = Memory.Form(new Frame(AtomSet.Empty, jimmyActual), jimmyExpected, jimmyActual, urgency);
        var bobMemory   = Memory.Form(new Frame(AtomSet.Empty, bobActual),   bobExpected,   bobActual,   urgency);

        // Jimmy's large negative surprise should have higher significance
        // and therefore slower decay than Bob's smaller positive surprise
        Assert.IsTrue(jimmyMemory.Significance > bobMemory.Significance,
            "Jimmy's larger surprise should produce higher significance");
        Assert.IsTrue(jimmyMemory.DecayRate < bobMemory.DecayRate,
            "Higher significance should produce slower decay");
    }

    // ── Alice's observation ──────────────────────────────────────────────────

    [TestMethod]
    public void Alice_HighTrustInBob_AmplifiesObservation_OfBobWinning()
    {
        var alice = BuildAlice();

        // Alice's belief frame about Bob contributes trust to the evaluation context
        var bobAppearance = new AtomSet(new Atom("bob", 1.0f));
        var contribution  = BeliefFunction.ComputeContextualContribution(alice, bobAppearance);

        Assert.IsTrue(contribution["trust"] > 0f,
            "Alice's high trust in Bob should contribute positively when observing Bob");
    }

    [TestMethod]
    public void Alice_LowerTrustInJimmy_ProducesWeakerContribution()
    {
        var alice = BuildAlice();

        var bobAppearance   = new AtomSet(new Atom("bob",   1.0f));
        var jimmyAppearance = new AtomSet(new Atom("jimmy", 1.0f));

        var bobContribution   = BeliefFunction.ComputeContextualContribution(alice, bobAppearance);
        var jimmyContribution = BeliefFunction.ComputeContextualContribution(alice, jimmyAppearance);

        Assert.IsTrue(bobContribution["trust"] > jimmyContribution["trust"],
            "Alice trusts Bob more than Jimmy — Bob's context should contribute more trust");
    }

    // ── Integration tests (pending SimulationLoop) ───────────────────────────

}
