using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 6 — The Traumatic Event
/// A violent event occurs in front of witnesses.
/// Tests fight-or-flight emergence from urgency scoring, high-significance
/// memory formation, disposition drift over time, observer belief updates,
/// and compassion as a disposition.
/// </summary>
[TestClass]
public class TheTraumaticEventTests
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
            // Disposition: observed threat raises safety urgency exponent
            .WithFrame(new Frame(
                new AtomSet(new Atom("threat", 0.3f)),
                new AtomSet(new Atom("safety", 0.6f))))
            // Prior belief about Jimmy — negative safety association
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("safety", -0.4f))));

    private static Agent BuildAlice() =>
        new Agent("alice")
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("pain", 0.0f), new Atom("safety", 0.9f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("pain", 0.0f), new Atom("safety", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("pain", 2.0f), new Atom("safety", 2.0f))))
            // Trust frames
            .WithFrame(new Frame(
                new AtomSet(new Atom("bob", 1.0f)),
                new AtomSet(new Atom("trust", 0.8f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("trust", 0.4f))))
            // Compassion disposition: observed pain contributes to own pain state
            .WithFrame(new Frame(
                new AtomSet(new Atom("pain", 0.3f)),
                new AtomSet(new Atom("pain", 0.4f))));

    private static AtomSet JimmyAppearance =>
        new AtomSet(new Atom("jimmy", 1.0f), new Atom("anger", 0.9f), new Atom("threat", 0.8f));

    // ── Fight-or-flight via urgency ──────────────────────────────────────────

    [TestMethod]
    public void ThreatDisposition_FiresOnJimmyAppearance()
    {
        var bob = BuildBob();

        // Jimmy's appearance contains ("threat", 0.8f) which exceeds disposition threshold 0.3f
        var contribution = BeliefFunction.ComputeContextualContribution(bob, JimmyAppearance);

        // Disposition contributes ("safety", 0.6f) — raises urgency exponent
        // Jimmy belief contributes ("safety", -0.4f) — suppresses expected safety
        // Net safety contribution should be non-zero
        Assert.IsTrue(contribution["safety"] != 0f,
            "Threat disposition and Jimmy belief should both contribute to safety context");
    }

    [TestMethod]
    public void JimmyBeliefFrame_ContributesNegativeSafety()
    {
        var bob = BuildBob();
        var jimmyOnlyAppearance = new AtomSet(new Atom("jimmy", 1.0f));

        var contribution = BeliefFunction.ComputeContextualContribution(bob, jimmyOnlyAppearance);

        Assert.IsTrue(contribution["safety"] < 0f,
            "Bob's prior negative belief about Jimmy should contribute negative safety");
    }

    [TestMethod]
    public void FleeAction_ScoresHigher_ThanDoNothing_WhenThreatPresent()
    {
        // Safety urgency after threat disposition fires
        // Base: delta = 1.0 - 0.88 = 0.12, exponent 2.0 → urgency 0.0144
        // With threat disposition contribution of 0.6 added to exponent: 0.12^2.6 ≈ 0.006
        // However the negative safety contribution from Jimmy belief means
        // flee (positive safety offer) scores better than doing nothing (zero offer)
        var urgencyProfile = new AtomSet(new Atom("safety", 0.3f), new Atom("pain", 0.0f));

        var fleeAdvertisement     = new AtomSet(new Atom("safety", 0.5f));
        var doNothingAdvertisement = AtomSet.Empty;

        var fleeScore     = SimilarityFunction.Score(urgencyProfile, fleeAdvertisement);
        var doNothingScore = SimilarityFunction.Score(urgencyProfile, doNothingAdvertisement);

        Assert.IsTrue(fleeScore > doNothingScore,
            $"Flee ({fleeScore}) should score higher than do nothing ({doNothingScore})");
    }

    // ── Traumatic memory formation ───────────────────────────────────────────

    [TestMethod]
    public void Bob_FormsHighSignificanceNegativeMemory_AfterViolence()
    {
        var expected = new AtomSet(new Atom("safety", 0.88f), new Atom("pain", 0.0f));
        var actual   = new AtomSet(new Atom("safety", 0.28f), new Atom("pain", 0.7f));
        var urgency  = new AtomSet(new Atom("safety", 0.7225f), new Atom("pain", 0.49f));

        var memory = Memory.Form(
            frame: new Frame(JimmyAppearance, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgency);

        Assert.IsTrue(memory.Significance > 0f, "Memory should have significant weight");
        Assert.IsTrue(memory.DecayRate < 1f,    "Traumatic memory should decay slowly");
    }

    [TestMethod]
    public void Bob_TraumaticMemory_HasNegativeSurpriseDelta_OnSafety()
    {
        var expected = new AtomSet(new Atom("safety", 0.88f), new Atom("pain", 0.0f));
        var actual   = new AtomSet(new Atom("safety", 0.28f), new Atom("pain", 0.7f));
        var urgency  = new AtomSet(new Atom("safety", 0.7225f), new Atom("pain", 0.49f));

        var memory = Memory.Form(
            frame: new Frame(JimmyAppearance, actual),
            expected: expected,
            actual: actual,
            urgencyProfile: urgency);

        // safety surprise = 0.28 - 0.88 = -0.6 (negative — worse than expected)
        // pain surprise   = 0.7  - 0.0  = +0.7 (positive delta but bad — pain increased)
        Assert.AreEqual(-0.6f, memory.SurpriseDelta["safety"], 0.001f);
        Assert.AreEqual(0.7f,  memory.SurpriseDelta["pain"],   0.001f);
    }

    [TestMethod]
    public void TraumaticMemory_IsMoreSignificant_ThanMundaneMemory()
    {
        var traumaExpected = new AtomSet(new Atom("safety", 0.88f), new Atom("pain", 0.0f));
        var traumaActual   = new AtomSet(new Atom("safety", 0.28f), new Atom("pain", 0.7f));
        var traumaUrgency  = new AtomSet(new Atom("safety", 0.7225f), new Atom("pain", 0.49f));

        var mundaneExpected = new AtomSet(new Atom("hunger", 0.6f));
        var mundaneActual   = new AtomSet(new Atom("hunger", 0.65f));
        var mundaneUrgency  = new AtomSet(new Atom("hunger", 0.16f));

        var traumaMemory  = Memory.Form(new Frame(AtomSet.Empty, traumaActual),  traumaExpected,  traumaActual,  traumaUrgency);
        var mundaneMemory = Memory.Form(new Frame(AtomSet.Empty, mundaneActual), mundaneExpected, mundaneActual, mundaneUrgency);

        Assert.IsTrue(traumaMemory.Significance > mundaneMemory.Significance,
            "Traumatic memory should be significantly more significant than a mundane one");
        Assert.IsTrue(traumaMemory.DecayRate < mundaneMemory.DecayRate,
            "Traumatic memory should decay slower than mundane memory");
    }

    // ── Alice's observation ──────────────────────────────────────────────────

    [TestMethod]
    public void Alice_HighTrustInBob_AmplifiesObservationOfViolenceAgainstHim()
    {
        var alice = BuildAlice();
        var bobAppearance   = new AtomSet(new Atom("bob",   1.0f));
        var jimmyAppearance = new AtomSet(new Atom("jimmy", 1.0f));

        var bobContribution   = BeliefFunction.ComputeContextualContribution(alice, bobAppearance);
        var jimmyContribution = BeliefFunction.ComputeContextualContribution(alice, jimmyAppearance);

        Assert.IsTrue(bobContribution["trust"]   > jimmyContribution["trust"],
            "Alice trusts Bob more — observation of violence against Bob should be weighted more strongly");
    }

    [TestMethod]
    public void Alice_CompassionDisposition_FiresOnObservedPain()
    {
        var alice = BuildAlice();

        // Bob's appearance after violence includes high pain
        var bobPostViolenceAppearance = new AtomSet(
            new Atom("bob",  1.0f),
            new Atom("pain", 0.7f));

        var contribution = BeliefFunction.ComputeContextualContribution(alice, bobPostViolenceAppearance);

        // Alice's compassion disposition: context ("pain", 0.3f) → value ("pain", 0.4f)
        // Bob's pain of 0.7f exceeds threshold — compassion fires
        Assert.IsTrue(contribution["pain"] > 0f,
            "Alice's compassion disposition should fire when observing Bob's pain");
    }

    [TestMethod]
    public void Alice_CompassionDisposition_DoesNotFire_WithoutObservedPain()
    {
        var alice = BuildAlice();

        // Bob appears fine — no pain atoms
        var bobNormalAppearance = new AtomSet(new Atom("bob", 1.0f), new Atom("social", 0.8f));
        var contribution = BeliefFunction.ComputeContextualContribution(alice, bobNormalAppearance);

        Assert.AreEqual(0f, contribution["pain"], 0.001f,
            "Compassion disposition should not fire without observed pain");
    }    

    // ── Axiom 5 — Learning and disposition drift ─────────────────────────────

    [TestMethod]
    public void Bob_DispositionAboutJimmy_StrengthensWithRepeatedViolentMemories()
    {
        var bob = BuildBob()
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
                AtomSet.Empty,
                Default: 0.001f))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.LearningRate, 1f)),
                AtomSet.Empty,
                Default: 0.1f));

        // Seed a violent memory about Jimmy
        var violentMemory = Memory.Form(
            frame: new Frame(
                new AtomSet(new Atom("jimmy", 1.0f)),
                new AtomSet(new Atom("violent", 0.7f))),
            expected: new AtomSet(new Atom("safety", 0.88f)),
            actual:   new AtomSet(new Atom("safety", 0.28f), new Atom("pain", 0.7f)),
            urgencyProfile: new AtomSet(new Atom("safety", 0.7225f), new Atom("pain", 0.49f)));

        bob = bob.WithMemory(violentMemory);

        var env  = new TestEnvironment();
        var loop = new SimulationLoop(env, [bob]);
        loop.Step();
        loop.Step();
        loop.Step();

        var updatedBob = loop.Agents.First(a => a.Id == "bob");

        var jimmyDisposition = updatedBob.BeliefFrames
            .FirstOrDefault(f => f.Context.Contains("jimmy"));

        Assert.IsNotNull(jimmyDisposition,
            "Bob should have a belief frame about Jimmy after repeated violent memories");
        Assert.IsTrue(jimmyDisposition.Value["violent"] > 0f,
            "Bob's disposition should encode Jimmy as violent");
    }

    [TestMethod]
    public void Bob_DispositionAboutJimmy_FadesWithoutReinforcement()
    {
        var jimmyDisposition = new Frame(
            new AtomSet(new Atom("jimmy", 1.0f)),
            new AtomSet(new Atom("violent", 0.5f)));

        var bob = BuildBob()
            .WithFrame(jimmyDisposition)
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f), new Atom(CollectionRoles.Belief, 1f)),
                AtomSet.Empty,
                Default: 0.1f)); // fast decay for test purposes

        var env  = new TestEnvironment();
        var loop = new SimulationLoop(env, [bob]);

        for (int i = 0; i < 5; i++) loop.Step();

        var updatedBob = loop.Agents.First(a => a.Id == "bob");
        var updatedDisposition = updatedBob.BeliefFrames
            .FirstOrDefault(f => f.Context.Contains("jimmy"));

        var violentMagnitude = updatedDisposition?.Value["violent"] ?? 0f;
        Assert.IsTrue(violentMagnitude < 0.5f,
            $"Bob's violent disposition about Jimmy should have faded (magnitude: {violentMagnitude})");
    }
}
