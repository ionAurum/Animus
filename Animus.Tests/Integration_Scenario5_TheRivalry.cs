using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Integration tests — Scenario 5 — The Rivalry
/// Tests reservation after arbitration and belief erosion over time.
/// </summary>
[TestClass]
public class Integration_Scenario5_TheRivalry
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
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("hunger", 0.8f))))
            // Negative belief about Jimmy near food
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
                new AtomSet(new Atom("hunger", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("hunger", 0.8f))));

    private static Agent LastMeal => new Agent("last_meal")
        .WithFrame(new Frame(
            new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
            new AtomSet(new Atom("last_meal", 1.0f))));

    [TestMethod]
    public void LastMeal_ResolutionApplied_AfterBobWinsArbitration()
    {
        var lastMeal  = LastMeal;
        var lastMealAd = new Advertisement(
            Source: lastMeal,
            Offered: new AtomSet(new Atom("hunger", 0.8f)),
            SourceAppearance: new AtomSet(new Atom("last_meal", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob",   lastMealAd)
            .WithStaticAdvertisements("jimmy", lastMealAd);

        var loop = new SimulationLoop(env, [BuildBob(), BuildJimmy(), lastMeal]);
        loop.Step();

        // Bob has higher urgency — should win arbitration
        var bobResolution   = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "bob");
        var jimmyResolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "jimmy");

        Assert.IsNotNull(bobResolution,   "Bob should have a resolution");
        Assert.IsNotNull(jimmyResolution, "Jimmy should have a resolution");
        Assert.IsTrue(bobResolution.Succeeded,    "Bob should win Last Meal");
        Assert.IsFalse(jimmyResolution.Succeeded, "Jimmy should lose Last Meal");
    }

    [TestMethod]
    public void Bob_HungerIncreases_AfterWinningLastMeal()
    {
        var lastMeal  = LastMeal;
        var lastMealAd = new Advertisement(
            Source: lastMeal,
            Offered: new AtomSet(new Atom("hunger", 0.8f)),
            SourceAppearance: new AtomSet(new Atom("last_meal", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob",   lastMealAd)
            .WithStaticAdvertisements("jimmy", lastMealAd);

        var loop = new SimulationLoop(env, [BuildBob(), BuildJimmy(), lastMeal]);
        loop.Step();

        var bob = loop.Agents.First(a => a.Id == "bob");

        // After decay: 0.15 - 0.1 (no decay defined — hunger stays at 0.15 after decay step)
        // After eating: 0.15 + min(0.8, absorption 0.8) = 0.95
        Assert.IsTrue(bob.State["hunger"] > 0.15f,
            "Bob's hunger should increase after winning Last Meal");
    }

    [TestMethod]
    public void Jimmy_FormsNegativeMemory_AfterLosingArbitration()
    {
        var lastMeal  = LastMeal;
        var lastMealAd = new Advertisement(
            Source: lastMeal,
            Offered: new AtomSet(new Atom("hunger", 0.8f)),
            SourceAppearance: new AtomSet(new Atom("last_meal", 1.0f)),
            RequiresConsent: false);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob",   lastMealAd)
            .WithStaticAdvertisements("jimmy", lastMealAd);

        var loop = new SimulationLoop(env, [BuildBob(), BuildJimmy(), lastMeal]);
        loop.Step();

        var jimmy = loop.Agents.First(a => a.Id == "jimmy");
        Assert.IsTrue(jimmy.Memories.Count > 0, "Jimmy should have formed a memory from the failed attempt");

        var memory = jimmy.Memories[0];
        Assert.IsTrue(memory.SurpriseDelta["hunger"] < 0f,
            "Jimmy's memory should have negative surprise — expected food, got nothing");
    }

    [TestMethod]
    public void BobNegativeBeliefAboutJimmy_Erodes_AfterPositiveSurprise()
    {
        // Bob expects reduced satisfaction due to Jimmy (negative belief) but gets
        // full delivery because Jimmy lost arbitration.
        // The perception tree must include Jimmy so his context affects Bob's memory.

        var lastMeal  = LastMeal;
        var jimmy     = BuildJimmy();

        var lastMealAd = new Advertisement(
            Source: lastMeal,
            Offered: new AtomSet(new Atom("hunger", 0.8f)),
            SourceAppearance: new AtomSet(new Atom("last_meal", 1.0f)),
            RequiresConsent: false);

        // Provide a perception tree that includes Jimmy — so Bob's memory
        // context captures Jimmy's presence during the encounter
        var jimmyObservation = new Observation(
            Subject: new AtomSet(new Atom("jimmy", 1.0f), new Atom("hunger", 0.6f)),
            Interaction: new AtomSet(new Atom("competes", 1.0f)),
            Object: new AtomSet(new Atom("last_meal", 1.0f)));

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob",   lastMealAd)
            .WithStaticAdvertisements("jimmy", lastMealAd)
            .WithStaticPerception("bob", jimmyObservation);

        var loop = new SimulationLoop(env, [BuildBob(), jimmy, lastMeal]);
        loop.Step();

        var bob = loop.Agents.First(a => a.Id == "bob");

        Assert.IsTrue(bob.Memories.Count > 0,
            "Bob should have formed a memory");

        var memory = bob.Memories[0];

        Assert.IsTrue(memory.SurpriseDelta["hunger"] > 0f,
            $"Bob should have positive surprise — got more than expected (surprise: {memory.SurpriseDelta["hunger"]})");
    }
}
