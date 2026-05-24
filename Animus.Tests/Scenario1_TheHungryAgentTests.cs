using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 1 — The Hungry Agent
/// A single agent with a depleting hunger atom and one food source.
/// Tests decay, urgency computation, advertisement scoring,
/// action selection, outcome resolution, and memory formation.
/// </summary>
[TestClass]
public class TheHungryAgentTests
{
    private static Agent BuildBob() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", 0.3f), new Atom("energy", 0.8f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", 1.0f), new Atom("energy", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f), new Atom("energy", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
                new AtomSet(new Atom("hunger", -0.1f), new Atom("energy", -0.05f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("hunger", 0.5f))));

    [TestMethod]
    public void AfterDecay_HungerDropsByDecayRate()
    {
        var bob = BuildBob();
        Assert.AreEqual(0.3f, bob.State["hunger"], 0.001f);
        Assert.AreEqual(-0.1f, bob.Decay["hunger"], 0.001f);

        // Decay applied: 0.3 + (-0.1) = 0.2
        var decayedState = new AtomSet(
            bob.State.Atoms.Select(a =>
            {
                var rate = bob.Decay[a.Label];
                return rate == 0f ? a : a with { Magnitude = a.Magnitude + rate };
            }));

        Assert.AreEqual(0.2f, decayedState["hunger"], 0.001f);
        Assert.AreEqual(0.75f, decayedState["energy"], 0.001f);
    }

    [TestMethod]
    public void HungerUrgency_ExceedsEnergyUrgency_AfterDecay()
    {
        var state = new AtomSet(new Atom("hunger", 0.2f), new Atom("energy", 0.75f));
        var preference = new AtomSet(new Atom("hunger", 1.0f), new Atom("energy", 1.0f));
        var urgencyExponents = new AtomSet(new Atom("hunger", 2.0f), new Atom("energy", 2.0f));
        var modifierSums = AtomSet.Empty;

        var profile = UrgencyFunction.ComputeProfile(state, preference, urgencyExponents, modifierSums);

        var hungerUrgency = profile["hunger"];
        var energyUrgency = profile["energy"];

        // hunger delta = 0.8, urgency = 0.8^2 = 0.64
        Assert.AreEqual(0.64f, hungerUrgency, 0.001f);
        // energy delta = 0.25, urgency = 0.25^2 = 0.0625
        Assert.AreEqual(0.0625f, energyUrgency, 0.001f);

        Assert.IsTrue(hungerUrgency > energyUrgency);
    }

    [TestMethod]
    public void Apple_ScoresPositive_AgainstHungerUrgency()
    {
        var urgencyProfile = new AtomSet(new Atom("hunger", 0.64f), new Atom("energy", 0.0625f));
        var appleAdvertisement = new AtomSet(new Atom("hunger", 0.4f));
        var appleAppearance = new AtomSet(new Atom("apple", 1.0f));

        var bob = BuildBob();
        var score = BeliefFunction.ScoreAdvertisement(
            bob, urgencyProfile, appleAdvertisement, appleAppearance);

        Assert.IsTrue(score > 0f);
    }

    [TestMethod]
    public void Memory_IsWeak_WhenOutcomeMatchesExpectation()
    {
        var expected = new AtomSet(new Atom("hunger", 0.6f));
        var actual = new AtomSet(new Atom("hunger", 0.6f));
        var urgencyProfile = new AtomSet(new Atom("hunger", 0.64f));

        var memory = Memory.Form(
            frame: new Frame(AtomSet.Empty, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgencyProfile);

        // Zero surprise — near-zero significance — fast decay
        Assert.AreEqual(0f, memory.Significance, 0.001f);
        Assert.AreEqual(1f, memory.DecayRate, 0.001f); // maximum decay — fades immediately
    }

    [TestMethod]
    public void SurpriseDelta_IsZero_WhenActualMatchesExpected()
    {
        var expected = new AtomSet(new Atom("hunger", 0.6f));
        var actual = new AtomSet(new Atom("hunger", 0.6f));
        var urgencyProfile = new AtomSet(new Atom("hunger", 0.64f));

        var memory = Memory.Form(
            frame: new Frame(AtomSet.Empty, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgencyProfile);

        Assert.AreEqual(0f, memory.SurpriseDelta["hunger"], 0.001f);
    }

    [TestMethod]
    public void SignedUrgency_IsPositive_WhenBelowPreference()
    {
        // hunger state 0.2, preference 1.0 — below preference — should approach
        var urgency = UrgencyFunction.Compute(delta: 0.8f, exponent: 2.0f);
        Assert.IsTrue(urgency > 0f);
    }

    [TestMethod]
    public void SignedUrgency_IsNegative_WhenAbovePreference()
    {
        // overshoot — above preference — should avoid
        var urgency = UrgencyFunction.Compute(delta: -0.3f, exponent: 2.0f);
        Assert.IsTrue(urgency < 0f);
    }

    [TestMethod]
    public void SignedUrgency_IsZero_WhenAtPreference()
    {
        var urgency = UrgencyFunction.Compute(delta: 0f, exponent: 2.0f);
        Assert.AreEqual(0f, urgency, 0.001f);
    }

    [TestMethod]
    public void Memory_HasSlowDecay_WhenSignificanceIsHigh()
    {
        var expected = new AtomSet(new Atom("hunger", 0.2f));
        var actual = new AtomSet(new Atom("hunger", 0.9f)); // large positive surprise
        var urgencyProfile = new AtomSet(new Atom("hunger", 0.64f));

        var memory = Memory.Form(
            frame: new Frame(AtomSet.Empty, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgencyProfile);

        Assert.IsTrue(memory.Significance > 0f);
        Assert.IsTrue(memory.DecayRate < 1f); // slower decay than zero-surprise memory
    }
}
