using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 2 — Two Needs, One Tick
/// A single agent with two active needs and two available satisfiers.
/// Tests competing urgency profiles and scoring function conflict resolution.
/// </summary>
[TestClass]
public class TwoNeedsOneTickTests
{
    private static Agent BuildBob(
        float hungerState = 0.3f,
        float sleepState  = 0.2f,
        float hungerPref  = 1.0f,
        float sleepPref   = 1.0f) =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", hungerState), new Atom("sleep", sleepState))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", hungerPref), new Atom("sleep", sleepPref))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f), new Atom("sleep", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
                new AtomSet(new Atom("hunger", -0.05f), new Atom("sleep", -0.05f))));

    // ── Decay ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void AfterDecay_BothNeedsDecrease()
    {
        var bob = BuildBob();

        var decayedState = new AtomSet(
            bob.State.Atoms.Select(a =>
            {
                var rate = bob.Decay[a.Label];
                return rate == 0f ? a : a with { Magnitude = a.Magnitude + rate };
            }));

        Assert.AreEqual(0.25f, decayedState["hunger"], 0.001f);
        Assert.AreEqual(0.15f, decayedState["sleep"],  0.001f);
    }

    // ── Urgency ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void SleepUrgency_ExceedsHungerUrgency_AfterDecay()
    {
        // Post-decay state
        var state      = new AtomSet(new Atom("hunger", 0.25f), new Atom("sleep", 0.15f));
        var preference = new AtomSet(new Atom("hunger", 1.0f),  new Atom("sleep", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f),  new Atom("sleep", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        // hunger delta = 0.75, urgency = 0.75^2 = 0.5625
        // sleep  delta = 0.85, urgency = 0.85^2 = 0.7225
        Assert.AreEqual(0.5625f, profile["hunger"], 0.001f);
        Assert.AreEqual(0.7225f, profile["sleep"],  0.001f);
        Assert.IsTrue(profile["sleep"] > profile["hunger"]);
    }

    // ── Scoring ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Bed_ScoresHigherThanApple_WhenSleepUrgencyDominates()
    {
        var bob = BuildBob();
        var urgencyProfile = new AtomSet(
            new Atom("hunger", 0.5625f),
            new Atom("sleep",  0.7225f));

        var appleAdvertisement = new AtomSet(new Atom("hunger", 0.5f));
        var bedAdvertisement   = new AtomSet(new Atom("sleep",  0.6f));
        var appleAppearance    = new AtomSet(new Atom("apple", 1.0f));
        var bedAppearance      = new AtomSet(new Atom("bed",   1.0f));

        var appleScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, appleAdvertisement, appleAppearance);
        var bedScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, bedAdvertisement, bedAppearance);

        Assert.IsTrue(bedScore > appleScore,
            $"Expected bed ({bedScore}) > apple ({appleScore})");
    }

    [TestMethod]
    public void Apple_ScoresHigherThanBed_WhenHungerUrgencyDominates()
    {
        var bob = BuildBob(hungerState: 0.1f, sleepState: 0.9f);

        // hunger delta = 0.9, urgency = 0.81
        // sleep  delta = 0.1, urgency = 0.01
        var urgencyProfile = new AtomSet(
            new Atom("hunger", 0.81f),
            new Atom("sleep",  0.01f));

        var appleAdvertisement = new AtomSet(new Atom("hunger", 0.5f));
        var bedAdvertisement   = new AtomSet(new Atom("sleep",  0.6f));
        var appleAppearance    = new AtomSet(new Atom("apple", 1.0f));
        var bedAppearance      = new AtomSet(new Atom("bed",   1.0f));

        var appleScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, appleAdvertisement, appleAppearance);
        var bedScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, bedAdvertisement, bedAppearance);

        Assert.IsTrue(appleScore > bedScore,
            $"Expected apple ({appleScore}) > bed ({bedScore})");
    }

    // ── Preference magnitude as priority weight ───────────────────────────────

    [TestMethod]
    public void HigherPreferenceMagnitude_ProducesHigherUrgency_AtSameState()
    {
        // Same state, same urgency exponent — preference magnitude breaks the tie
        var state      = new AtomSet(new Atom("hunger", 0.2f), new Atom("sleep", 0.2f));
        var preference = new AtomSet(new Atom("hunger", 1.5f), new Atom("sleep", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f), new Atom("sleep", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        // hunger delta = 1.3, urgency = 1.3^2 = 1.69
        // sleep  delta = 0.8, urgency = 0.8^2 = 0.64
        Assert.AreEqual(1.69f, profile["hunger"], 0.001f);
        Assert.AreEqual(0.64f, profile["sleep"],  0.001f);
        Assert.IsTrue(profile["hunger"] > profile["sleep"]);
    }

    [TestMethod]
    public void HigherPreferenceMagnitude_CausesAppleToScoreHigher_ThanBed()
    {
        // Bob has high hunger preference — hunger wins despite equal state
        var bob = BuildBob(hungerState: 0.2f, sleepState: 0.2f, hungerPref: 1.5f, sleepPref: 1.0f);

        var urgencyProfile = new AtomSet(
            new Atom("hunger", 1.69f),
            new Atom("sleep",  0.64f));

        var appleAdvertisement = new AtomSet(new Atom("hunger", 0.5f));
        var bedAdvertisement   = new AtomSet(new Atom("sleep",  0.6f));
        var appleAppearance    = new AtomSet(new Atom("apple", 1.0f));
        var bedAppearance      = new AtomSet(new Atom("bed",   1.0f));

        var appleScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, appleAdvertisement, appleAppearance);
        var bedScore = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, bedAdvertisement, bedAppearance);

        Assert.IsTrue(appleScore > bedScore,
            $"Expected apple ({appleScore}) > bed ({bedScore})");
    }

    // ── Signed urgency — overshoot avoidance ────────────────────────────────

    [TestMethod]
    public void Sleep_ProducesNegativeUrgency_WhenAbovePreference()
    {
        // Bob has too much sleep — should avoid sleep-providing advertisements
        var state      = new AtomSet(new Atom("hunger", 0.5f), new Atom("sleep", 1.3f));
        var preference = new AtomSet(new Atom("hunger", 1.0f), new Atom("sleep", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f), new Atom("sleep", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        Assert.IsTrue(profile["hunger"] > 0f, "Hunger should be positive — below preference");
        Assert.IsTrue(profile["sleep"]  < 0f, "Sleep should be negative — above preference");
    }
}
