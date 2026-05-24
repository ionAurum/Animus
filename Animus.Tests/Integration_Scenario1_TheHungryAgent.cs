using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Integration test — Scenario 1 — The Hungry Agent
/// Runs Bob through the full phased loop with a real environment.
/// Validates that decay, urgency, scoring, resolution, and memory
/// formation all work correctly end-to-end.
/// </summary>
[TestClass]
public class Integration_Scenario1_TheHungryAgent
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

    private static Agent Apple => new Agent("apple")
        .WithFrame(new Frame(
            new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
            new AtomSet(new Atom("apple", 1.0f))));

    [TestMethod]
    public void Bob_EatsApple_HungerIncreases_AfterOneTick()
    {
        var apple = Apple;
        var appleAd = new Advertisement(
            Source: apple,
            Offered: new AtomSet(new Atom("hunger", 0.4f)),
            SourceAppearance: new AtomSet(new Atom("apple", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", appleAd);

        var loop = new SimulationLoop(env, [BuildBob()]);
        loop.Step();

        var bob = loop.Agents[0];

        // After decay: hunger 0.3 - 0.1 = 0.2
        // After eating apple: 0.2 + min(0.4, absorption 0.5) = 0.6
        Assert.AreEqual(0.6f, bob.State["hunger"], 0.01f,
            "Bob's hunger should increase after eating the apple");
    }

    [TestMethod]
    public void Bob_FormsMemory_AfterEatingApple()
    {
        var apple = Apple;
        var appleAd = new Advertisement(
            Source: apple,
            Offered: new AtomSet(new Atom("hunger", 0.4f)),
            SourceAppearance: new AtomSet(new Atom("apple", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", appleAd);

        var loop = new SimulationLoop(env, [BuildBob()]);
        loop.Step();

        var bob = loop.Agents[0];
        Assert.IsTrue(bob.Memories.Count > 0, "Bob should have formed at least one memory");
    }

    [TestMethod]
    public void Bob_HungerKeepsDecaying_OverMultipleTicks_WithoutFood()
    {
        var env  = new TestEnvironment(); // no advertisements — Bob has nothing to eat
        var loop = new SimulationLoop(env, [BuildBob()]);

        loop.Step();
        loop.Step();
        loop.Step();

        var bob = loop.Agents[0];

        // After 3 ticks of decay at -0.1f: 0.3 - 0.3 = 0.0f (clamped or at zero)
        Assert.IsTrue(bob.State["hunger"] <= 0.1f,
            "Bob's hunger should keep decaying without food");
    }

    [TestMethod]
    public void Bob_AppearanceUpdates_AfterEatingApple()
    {
        var apple = Apple;
        var appleAd = new Advertisement(
            Source: apple,
            Offered: new AtomSet(new Atom("hunger", 0.4f)),
            SourceAppearance: new AtomSet(new Atom("apple", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", appleAd);

        var loop = new SimulationLoop(env, [BuildBob()]);
        loop.Step();

        var bob = loop.Agents[0];

        // Appearance should reflect updated state
        Assert.AreEqual(bob.State["hunger"], bob.Appearance["hunger"], 0.001f,
            "Bob's appearance should reflect his current hunger state");
    }

    [TestMethod]
    public void Environment_ReceivesResolution_AfterBobEatsApple()
    {
        var apple = Apple;
        var appleAd = new Advertisement(
            Source: apple,
            Offered: new AtomSet(new Atom("hunger", 0.4f)),
            SourceAppearance: new AtomSet(new Atom("apple", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", appleAd);

        var loop = new SimulationLoop(env, [BuildBob()]);
        loop.Step();

        Assert.IsTrue(env.AppliedResolutions.Count > 0,
            "Environment should have received at least one resolution");
        Assert.IsTrue(env.AppliedResolutions[0].Succeeded,
            "Resolution should have succeeded");
    }
}
