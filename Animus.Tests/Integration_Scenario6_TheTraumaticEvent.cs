using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Integration tests — Scenario 6 — The Traumatic Event
/// Tests fight-or-flight declaration and traumatic memory formation via loop.
/// </summary>
[TestClass]
public class Integration_Scenario6_TheTraumaticEvent
{
    private static Agent BuildBob() =>
        new Agent("bob")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("safety", 0.88f), new Atom("pain", 0.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("safety", 1.0f), new Atom("pain", 0.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("safety", 2.0f), new Atom("pain", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Absorption, 1f)),
                new AtomSet(new Atom("safety", 1.0f))))
            // Prior belief: Jimmy is bad for safety
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("safety", -0.4f))));

    private static Agent BuildSafePlace() =>
        new Agent("safe_place")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
                new AtomSet(new Atom("safe_place", 1.0f))));

    private static Agent BuildJimmy() =>
        new Agent("jimmy")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("anger", 0.9f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Appearance, 1f)),
                new AtomSet(new Atom("jimmy", 1.0f), new Atom("anger", 0.9f), new Atom("threat", 0.8f))));

    [TestMethod]
    public void Bob_SelectsFleeAction_OverViolentInteraction()
    {
        var safePlace = BuildSafePlace();
        var jimmy     = BuildJimmy();

        // Bob has two options: flee to safe place or interact with threatening Jimmy
        var fleeAd = new Advertisement(
            Source: safePlace,
            Offered: new AtomSet(new Atom("safety", 0.5f)),
            SourceAppearance: new AtomSet(new Atom("safe_place", 1.0f)),
            RequiresConsent: false);

        var jimmyAd = new Advertisement(
            Source: jimmy,
            Offered: new AtomSet(new Atom("safety", -0.6f), new Atom("pain", 0.7f)),
            SourceAppearance: jimmy.Appearance,
            RequiresConsent: true);

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", fleeAd, jimmyAd);

        var loop = new SimulationLoop(env, [BuildBob(), safePlace, jimmy]);
        loop.Step();

        // Bob should have declared intent on safe place, not Jimmy
        var bobResolution = env.AppliedResolutions.FirstOrDefault(r => r.Intent.Declarer.Id == "bob");
        Assert.IsNotNull(bobResolution, "Bob should have a resolution");
        Assert.AreEqual("safe_place", bobResolution.Intent.Target.Id,
            "Bob should flee to safe place rather than engage with threatening Jimmy");
    }

    [TestMethod]
    public void Bob_FormsHighSignificanceMemory_AfterViolentOutcome()
    {
        var jimmy = BuildJimmy();

        // Bob has only Jimmy's violent advertisement available — no escape
        // Violence is a passive negative advertisement — Bob doesn't choose it,
        // it happens to him. His expected outcome was neutral (no harm expected).
        // The actual outcome is severely negative — large surprise, high significance.
        var jimmyAd = new Advertisement(
            Source: jimmy,
            Offered: new AtomSet(new Atom("safety", -0.6f), new Atom("pain", 0.7f)),
            SourceAppearance: jimmy.Appearance,
            RequiresConsent: false); // passive violence — no consent needed

        // Provide perception tree with Jimmy's threat — Bob's interpreted context
        // will include Jimmy's negative safety contribution, making expected
        // slightly negative but actual much more negative — still large surprise
        var jimmyObservation = new Observation(
            Subject: new AtomSet(new Atom("jimmy", 1.0f), new Atom("anger", 0.9f), new Atom("threat", 0.8f)),
            Interaction: new AtomSet(new Atom("threatens", 1.0f)),
            Object: new AtomSet(new Atom("bob", 1.0f)));

        var env = new TestEnvironment()
            .WithStaticAdvertisements("bob", jimmyAd)
            .WithStaticPerception("bob", jimmyObservation);

        var loop = new SimulationLoop(env, [BuildBob(), jimmy]);
        loop.Step();

        var bob = loop.Agents.First(a => a.Id == "bob");

        Assert.IsTrue(bob.Memories.Count > 0, "Bob should have formed a memory");

        var memory = bob.Memories[0];

        Assert.IsTrue(memory.Significance > 0f,
            $"Memory should be significant (significance: {memory.Significance})");
        Assert.IsTrue(memory.DecayRate < 1f,
            "Traumatic memory should decay slowly");
        Assert.IsTrue(bob.State["pain"] > 0f,
            "Bob should be in pain after violence");
        Assert.IsTrue(bob.State["safety"] < 0.88f,
            "Bob's safety should have decreased after violence");
    }
}
