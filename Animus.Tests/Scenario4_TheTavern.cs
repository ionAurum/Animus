using Microsoft.VisualStudio.TestTools.UnitTesting;
using Animus.Core.Agents;
using Animus.Core.Engine;
using Animus.Core.Primitives;

namespace Animus.Tests;

/// <summary>
/// Scenario 4 — The Tavern
/// Multiple agents with varying needs interact in a shared space.
///
/// Unit tests cover: urgency computation per agent, advertisement scoring
/// showing dominant need selection, and urgency-weighted arbitration for
/// passive object contention.
///
/// Integration tests (pending SimulationLoop implementation):
/// - Barkeep active consent evaluation
/// - Multi-tick social emergence between agents who shared context
/// - Barkeep serving multiple agents at reduced rate
/// </summary>
[TestClass]
public class TheTavernTests
{
    // ── Agent builders ───────────────────────────────────────────────────────

    private static Agent BuildAgent(string id, float hunger, float thirst, float social) =>
        new Agent(id)
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.State, 1f)),
                new AtomSet(new Atom("hunger", hunger), new Atom("thirst", thirst), new Atom("social", social))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Preference, 1f)),
                new AtomSet(new Atom("hunger", 1.0f), new Atom("thirst", 1.0f), new Atom("social", 1.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Urgency, 1f)),
                new AtomSet(new Atom("hunger", 2.0f), new Atom("thirst", 2.0f), new Atom("social", 2.0f))))
            .WithFrame(new Frame(
                new AtomSet(new Atom(CollectionRoles.Decay, 1f)),
                new AtomSet(new Atom("hunger", -0.05f), new Atom("thirst", -0.05f), new Atom("social", -0.05f))));

    // Post-decay state values used throughout
    // Bob:   hunger 0.25, thirst 0.55, social 0.65
    // Alice: hunger 0.45, thirst 0.75, social 0.35
    // Carol: hunger 0.65, thirst 0.45, social 0.55

    private static Agent Bob   => BuildAgent("bob",   0.25f, 0.55f, 0.65f);
    private static Agent Alice => BuildAgent("alice", 0.45f, 0.75f, 0.35f);
    private static Agent Carol => BuildAgent("carol", 0.65f, 0.45f, 0.55f);

    private static AtomSet BarkeepAdvertisement  => new AtomSet(new Atom("thirst", 0.5f), new Atom("social", 0.3f));
    private static AtomSet FoodPlatterAdvertisement => new AtomSet(new Atom("hunger", 0.4f));
    private static AtomSet BarkeepAppearance     => new AtomSet(new Atom("barkeep", 1.0f));
    private static AtomSet FoodPlatterAppearance => new AtomSet(new Atom("food_platter", 1.0f));

    // ── Urgency computation ──────────────────────────────────────────────────

    [TestMethod]
    public void Bob_HungerUrgency_DominatesAfterDecay()
    {
        var state      = new AtomSet(new Atom("hunger", 0.25f), new Atom("thirst", 0.55f), new Atom("social", 0.65f));
        var preference = new AtomSet(new Atom("hunger", 1.0f),  new Atom("thirst", 1.0f),  new Atom("social", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f),  new Atom("thirst", 2.0f),  new Atom("social", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        // hunger delta=0.75 → 0.5625, thirst delta=0.45 → 0.2025, social delta=0.35 → 0.1225
        Assert.AreEqual(0.5625f, profile["hunger"], 0.001f);
        Assert.AreEqual(0.2025f, profile["thirst"], 0.001f);
        Assert.AreEqual(0.1225f, profile["social"], 0.001f);
        Assert.IsTrue(profile["hunger"] > profile["thirst"]);
        Assert.IsTrue(profile["hunger"] > profile["social"]);
    }

    [TestMethod]
    public void Alice_SocialUrgency_DominatesAfterDecay()
    {
        var state      = new AtomSet(new Atom("hunger", 0.45f), new Atom("thirst", 0.75f), new Atom("social", 0.35f));
        var preference = new AtomSet(new Atom("hunger", 1.0f),  new Atom("thirst", 1.0f),  new Atom("social", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f),  new Atom("thirst", 2.0f),  new Atom("social", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        // hunger delta=0.55 → 0.3025, thirst delta=0.25 → 0.0625, social delta=0.65 → 0.4225
        Assert.AreEqual(0.3025f, profile["hunger"], 0.001f);
        Assert.AreEqual(0.0625f, profile["thirst"], 0.001f);
        Assert.AreEqual(0.4225f, profile["social"], 0.001f);
        Assert.IsTrue(profile["social"] > profile["hunger"]);
        Assert.IsTrue(profile["social"] > profile["thirst"]);
    }

    [TestMethod]
    public void Carol_ThirstUrgency_DominatesAfterDecay()
    {
        var state      = new AtomSet(new Atom("hunger", 0.65f), new Atom("thirst", 0.45f), new Atom("social", 0.55f));
        var preference = new AtomSet(new Atom("hunger", 1.0f),  new Atom("thirst", 1.0f),  new Atom("social", 1.0f));
        var exponents  = new AtomSet(new Atom("hunger", 2.0f),  new Atom("thirst", 2.0f),  new Atom("social", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(state, preference, exponents, AtomSet.Empty);

        // hunger delta=0.35 → 0.1225, thirst delta=0.55 → 0.3025, social delta=0.45 → 0.2025
        Assert.AreEqual(0.1225f, profile["hunger"], 0.001f);
        Assert.AreEqual(0.3025f, profile["thirst"], 0.001f);
        Assert.AreEqual(0.2025f, profile["social"], 0.001f);
        Assert.IsTrue(profile["thirst"] > profile["hunger"]);
        Assert.IsTrue(profile["thirst"] > profile["social"]);
    }

    // ── Advertisement scoring ────────────────────────────────────────────────

    [TestMethod]
    public void Bob_FoodPlatter_ScoresHigherThanBarkeep()
    {
        var urgencyProfile = new AtomSet(
            new Atom("hunger", 0.5625f), new Atom("thirst", 0.2025f), new Atom("social", 0.1225f));

        var foodScore    = BeliefFunction.ScoreAdvertisement(Bob, urgencyProfile, FoodPlatterAdvertisement, FoodPlatterAppearance);
        var barkeepScore = BeliefFunction.ScoreAdvertisement(Bob, urgencyProfile, BarkeepAdvertisement,    BarkeepAppearance);

        Assert.IsTrue(foodScore > barkeepScore,
            $"Bob: food ({foodScore}) should beat barkeep ({barkeepScore})");
    }

    [TestMethod]
    public void Alice_Barkeep_ScoresHigherThanFoodPlatter()
    {
        var urgencyProfile = new AtomSet(
            new Atom("hunger", 0.3025f), new Atom("thirst", 0.0625f), new Atom("social", 0.4225f));

        var foodScore    = BeliefFunction.ScoreAdvertisement(Alice, urgencyProfile, FoodPlatterAdvertisement, FoodPlatterAppearance);
        var barkeepScore = BeliefFunction.ScoreAdvertisement(Alice, urgencyProfile, BarkeepAdvertisement,    BarkeepAppearance);

        Assert.IsTrue(barkeepScore > foodScore,
            $"Alice: barkeep ({barkeepScore}) should beat food ({foodScore})");
    }

    [TestMethod]
    public void Carol_Barkeep_ScoresHigherThanFoodPlatter()
    {
        var urgencyProfile = new AtomSet(
            new Atom("hunger", 0.1225f), new Atom("thirst", 0.3025f), new Atom("social", 0.2025f));

        var foodScore    = BeliefFunction.ScoreAdvertisement(Carol, urgencyProfile, FoodPlatterAdvertisement, FoodPlatterAppearance);
        var barkeepScore = BeliefFunction.ScoreAdvertisement(Carol, urgencyProfile, BarkeepAdvertisement,    BarkeepAppearance);

        Assert.IsTrue(barkeepScore > foodScore,
            $"Carol: barkeep ({barkeepScore}) should beat food ({foodScore})");
    }

    // ── Passive object arbitration ───────────────────────────────────────────

    [TestMethod]
    public void HigherUrgencyAgent_WinsPassiveObjectArbitration()
    {
        // Both Bob and Carol declare intent on Food Platter
        // Bob hunger urgency: 0.5625, Carol hunger urgency: 0.1225
        // Bob should win

        var bobTotalUrgency   = Bob.Preference.Labels
            .Select(l => MathF.Abs(UrgencyFunction.Compute(
                Bob.Preference[l] - Bob.State[l],
                Bob.Urgency[l] == 0f ? 1f : Bob.Urgency[l])))
            .Sum();

        var carolTotalUrgency = Carol.Preference.Labels
            .Select(l => MathF.Abs(UrgencyFunction.Compute(
                Carol.Preference[l] - Carol.State[l],
                Carol.Urgency[l] == 0f ? 1f : Carol.Urgency[l])))
            .Sum();

        Assert.IsTrue(bobTotalUrgency > carolTotalUrgency,
            $"Bob urgency ({bobTotalUrgency}) should exceed Carol ({carolTotalUrgency}) — Bob wins arbitration");
    }

    [TestMethod]
    public void Overshoot_ProducesNegativeUrgency_OnThirst()
    {
        // Alice thirst 0.75 + barkeep delivers 0.3 = 1.05 — overshoots preference
        var overshootState = new AtomSet(new Atom("thirst", 1.05f));
        var preference     = new AtomSet(new Atom("thirst", 1.0f));
        var exponents      = new AtomSet(new Atom("thirst", 2.0f));

        var profile = UrgencyFunction.ComputeProfile(overshootState, preference, exponents, AtomSet.Empty);

        Assert.IsTrue(profile["thirst"] < 0f,
            "Overshooting thirst preference should produce negative urgency — avoidance");
    }

    // ── Integration tests (pending SimulationLoop implementation) ────────────

}
