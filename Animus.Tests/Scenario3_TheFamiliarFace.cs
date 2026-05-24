using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 3 — The Familiar Face
/// Bob encounters Alice, whom he knows and likes but is currently angry at.
/// Tests belief frame chaining, anger modifier suppression, and modifier decay.
/// </summary>
[TestClass]
public class TheFamiliarFaceTests
{
    private static Agent BuildBob() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.5f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("social", 0.2f))))
            // Learned association: trust and warmth predict social value
            .WithFrame(new Frame(
                new AtomSet(new Atom("trust", 0.3f), new Atom("warmth", 0.5f)),
                new AtomSet(new Atom("social", 0.8f))))
            // Anger modifier toward Alice — decays at 0.3f per tick
            .WithModifier(new Modifier(
                Frame: new Frame(
                    new AtomSet(new Atom("alice", 1.0f)),
                    new AtomSet(new Atom("social", -0.6f))),
                Decay: new Frame(
                    new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
                    new AtomSet(new Atom("social", 0.3f)))));

    private static Agent BuildBobCalmed() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("social", 0.5f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("social", 0.2f))))
            // Same learned association — no anger modifier
            .WithFrame(new Frame(
                new AtomSet(new Atom("trust", 0.3f), new Atom("warmth", 0.5f)),
                new AtomSet(new Atom("social", 0.8f))));

    private static AtomSet AliceAppearance => new AtomSet(
        new Atom("alice",  1.0f),
        new Atom("trust",  0.7f),
        new Atom("warmth", 0.6f));

    private static AtomSet StrangerAppearance => new AtomSet(
        new Atom("stranger", 1.0f));

    private static AtomSet SocialAdvertisement => new AtomSet(
        new Atom("social", 0.3f));

    // ── Belief frame chaining ────────────────────────────────────────────────

    [TestMethod]
    public void LearnedAssociationFrame_HasPositiveGrossContribution_OnAliceAppearance()
    {
        var bob = BuildBob();

        // Check the learned association frame directly — in isolation from modifiers
        var learnedFrame = bob.BeliefFrames.First(f => f.Context.Contains("trust"));
        var similarity = SimilarityFunction.ComputeContextMatch(learnedFrame, AliceAppearance);
        var grossContribution = learnedFrame.Value["social"] * similarity;

        // The gross contribution should be positive — Alice exhibits trust and warmth
        // Net contribution through ComputeContextualContribution may be negative
        // due to the anger modifier — that is expected and correct behavior
        Assert.IsTrue(grossContribution > 0f,
            $"Learned association gross contribution should be positive (similarity: {similarity}, gross: {grossContribution})");
    }

    [TestMethod]
    public void LearnedAssociationFrame_DoesNotFire_OnStrangerAppearance()
    {
        var bob = BuildBob();

        var learnedFrame = bob.BeliefFrames.First(f => f.Context.Contains("trust"));
        var similarity = SimilarityFunction.ComputeContextMatch(learnedFrame, StrangerAppearance);

        // Stranger has no trust or warmth atoms — similarity should be zero or negative
        Assert.IsTrue(similarity <= 0f,
            "Learned association should not fire for a stranger with no trust/warmth atoms");
    }

    // ── Anger modifier ───────────────────────────────────────────────────────

    [TestMethod]
    public void AngerModifier_Suppresses_AliceNetContribution()
    {
        var bob = BuildBob();

        var learnedFrame = bob.BeliefFrames.First(f => f.Context.Contains("trust"));
        var similarity = SimilarityFunction.ComputeContextMatch(learnedFrame, AliceAppearance);
        var grossContribution = learnedFrame.Value["social"] * similarity;

        var netContribution = BeliefFunction.ComputeContextualContribution(bob, AliceAppearance);

        // Net should be less than gross due to anger modifier suppression
        Assert.IsTrue(netContribution["social"] < grossContribution,
            $"Anger modifier should suppress net contribution below gross ({netContribution["social"]} < {grossContribution})");
    }

    [TestMethod]
    public void AngerModifier_DoesNotFire_OnStrangerContext()
    {
        var bob = BuildBob();

        // Stranger has no alice atom — anger modifier context doesn't match
        var strangerContribution = BeliefFunction.ComputeContextualContribution(bob, StrangerAppearance);
        var aliceContribution    = BeliefFunction.ComputeContextualContribution(bob, AliceAppearance);

        // Stranger should not be suppressed by the Alice anger modifier
        Assert.IsTrue(strangerContribution["social"] >= aliceContribution["social"],
            "Anger modifier should not suppress Stranger's score");
    }

    // ── Advertisement scoring ────────────────────────────────────────────────

    [TestMethod]
    public void Alice_ScoresHigherThanStranger_WhenAngerIsDecayed()
    {
        // Once anger has decayed Bob should clearly prefer Alice
        var bobCalmed = BuildBobCalmed();
        var urgencyProfile = new AtomSet(new Atom("social", 0.25f));

        var aliceScore    = BeliefFunction.ScoreAdvertisement(bobCalmed, urgencyProfile, SocialAdvertisement, AliceAppearance);
        var strangerScore = BeliefFunction.ScoreAdvertisement(bobCalmed, urgencyProfile, SocialAdvertisement, StrangerAppearance);

        Assert.IsTrue(aliceScore > strangerScore,
            $"Expected alice ({aliceScore}) > stranger ({strangerScore}) once anger has decayed");
    }

    [TestMethod]
    public void Stranger_CanOutscoreAlice_WhenAngerIsStrong()
    {
        // At full anger magnitude Alice may score lower than Stranger — valid behavior
        var bob = BuildBob();
        var urgencyProfile = new AtomSet(new Atom("social", 0.25f));

        var aliceScore    = BeliefFunction.ScoreAdvertisement(bob, urgencyProfile, SocialAdvertisement, AliceAppearance);
        var strangerScore = BeliefFunction.ScoreAdvertisement(bob, urgencyProfile, SocialAdvertisement, StrangerAppearance);

        // This is not a failure — it validates that anger meaningfully suppresses Alice's score
        // Bob avoids Alice while angry; this is correct emergent behavior
        Assert.IsTrue(aliceScore < strangerScore,
            $"With full anger modifier Alice ({aliceScore}) should score below Stranger ({strangerScore})");
    }

    // ── Modifier decay ───────────────────────────────────────────────────────

    [TestMethod]
    public void AngerModifier_DecaysEachTick()
    {
        var bob = BuildBob();
        var modifier = bob.Modifiers[0];

        Assert.AreEqual(-0.6f, modifier.CurrentValue["social"], 0.001f);

        var afterOneTick  = modifier.ApplyDecay();
        Assert.AreEqual(-0.3f, afterOneTick.CurrentValue["social"], 0.001f);

        var afterTwoTicks = afterOneTick.ApplyDecay();
        Assert.AreEqual(0f, afterTwoTicks.CurrentValue["social"], 0.001f);
    }

    [TestMethod]
    public void AngerModifier_IsExpired_AfterFullDecay()
    {
        var modifier = BuildBob().Modifiers[0].ApplyDecay().ApplyDecay();
        Assert.IsTrue(modifier.IsExpired);
    }

    [TestMethod]
    public void Alice_ScoresHigherAfterAngerDecays()
    {
        var bob       = BuildBob();
        var bobCalmed = BuildBobCalmed();
        var urgencyProfile = new AtomSet(new Atom("social", 0.25f));

        var scoreWhileAngry = BeliefFunction.ScoreAdvertisement(bob,       urgencyProfile, SocialAdvertisement, AliceAppearance);
        var scoreCalmed     = BeliefFunction.ScoreAdvertisement(bobCalmed, urgencyProfile, SocialAdvertisement, AliceAppearance);

        Assert.IsTrue(scoreCalmed > scoreWhileAngry,
            $"Alice should score higher after anger decays ({scoreCalmed} > {scoreWhileAngry})");
    }
}
