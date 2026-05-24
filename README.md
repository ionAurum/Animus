# Animus

**A simulation engine for emergent narrative.**

Animus is a simulation engine built from a small set of universal primitives, where everything — objects, creatures, environments — is an agent with needs, and story emerges purely from agents continuously seeking to satisfy those needs. You set up the sandbox; the story writes itself.

---

## The Idea

Most game narrative is authored. Someone wrote the dialogue, plotted the beats, scripted the outcomes. Animus takes a different approach: narrative as a consequence of agents pursuing their own goals in a shared world.

The engine is inspired by:
- **GOAP** (Goal-Oriented Action Planning) — but without a centralized planner. Agents react locally rather than strategize globally.
- **The Sims** — specifically the "Smart Objects" / advertised interactables pattern, generalized.
- **God games** — the primary user sets up the world and watches what happens.

The result is a system where interesting, unscripted narrative emerges from agents interacting with each other and their environment. No authored dialogue. No predetermined plot. Just agents with needs, beliefs, and memories — doing what they need to do.

---

## Core Philosophy

### Everything is an Agent

People, animals, objects, environments — all are agents. The distinction between "intelligent" and "inanimate" is one of configuration complexity, not kind. A refrigerator and a person exist on the same continuum; they differ only in how richly they're configured.

### Three Primitives

The entire system is built from three primitives:

**Atom** — `(string label, float magnitude)`. The universal semantic primitive. Ruthlessly dumb — carries no self-knowledge of its role. Meaning is conferred entirely by which collection it lives in and what operates on it.

**AtomSet** — a collection of atoms. Has no intrinsic meaning; meaning is conferred by context.

**Frame** — `{ context: AtomSet, value: AtomSet, default?: float }`. A payload with a description of what it's about. The basis for beliefs, memories, modifiers, advertisements, and anything else that needs to say "this value applies *in this context*."

Everything else in the engine is composed from these three things.

### Self-Similarity

The same primitive structures recur at every level. Needs, perceptions, beliefs, memories, urgency curves, advertisements — all expressed as atoms and frames. This self-similarity is what enables elegant emergence: small, consistent rules produce complex behavior.

### Five Axioms

1. **The Prime Directive** — Every agent perpetually acts to minimize the aggregate delta between its current state atoms and its preference atoms.
2. **Equilibrium** — The theoretical state where all state atoms match preference atoms. Agents move toward it and likely never fully reach it. A fully equilibrated agent has no motivation to act.
3. **The Gravity Axiom** — `urgency(atom) = sign(delta) × |delta| ^ (baseExponent + Σ urgencyModifiers)`. The sign is preserved — positive urgency means approach, negative means avoidance. The exponent shape defines personality.
4. **The Stability Axiom** — The simulation must be inherently mathematically stable. No combination of atoms, modifiers, or modifier chains may produce unbounded runaway values.
5. **The Impermanence Axiom** — No modifier persists in its current form indefinitely. All modifiers must decay, collapse, or transform over time.

### Two Design Principles

**The Closed Algorithm** — The evaluation step function is closed. New nuance and complexity are expressed by enriching the inputs — atoms, modifiers, beliefs — never by adding steps to the algorithm. If a new step seems necessary, that is a signal that something belongs in the atom space that isn't there yet.

**The Emergence Principle** — Named psychological or social concepts — trust, love, fear, loyalty — are observer labels for emergent behavioral patterns. They are not first-class atoms or mechanisms. If the model is working correctly, these concepts emerge from the interaction of atoms, beliefs, and memories without being explicitly represented.

---

## How It Works

### Agents and Their Frame-Sets

An agent is a list of frames. The engine identifies and operates on frames whose context contains well-known collection role atoms:

| Collection | Role |
|---|---|
| `("state", 1f)` | Current state values |
| `("preference", 1f)` | Target values — what the agent wants |
| `("urgency", 1f)` | Urgency exponents for the Gravity Axiom |
| `("decay", 1f)` | Per-tick erosion rates |
| `("absorption", 1f)` | Maximum intake rate per atom |
| `("appearance", 1f)` | Projected outward state — the agent's advertisement |
| `("carried", 1f)` | Objects physically on the agent's person |
| `("owned", 1f)` | Objects the agent has a claim on |
| `("belief", 1f)` | Belief frame decay rate context |
| `("learning_rate", 1f)` | Per-atom learning rates |

Unknown frames are ignored cleanly by the core engine — they may be meaningful to sub-engines or future extensions.

### The Evaluation Step

Every agent runs the same loop every tick:

1. **Apply decay** — erode state atoms, modifiers, memories, and belief frames
2. **Receive perception tree** — the environment delivers an already-attenuated observation tree
3. **Interpret perception tree** — run the belief function against the observation
4. **Update beliefs** — fold new observations into the belief frame-set
5. **Compute urgency** — apply the Gravity Axiom across all state/preference deltas
6. **Score available actions** — similarity function against urgency profile, then contextual belief layers
7. **Declare intent** — commit to the highest-scoring action
8. **Resolve** — consent for active agents, urgency-weighted arbitration for passive objects
9. **Form memories** — compare expected vs actual, encode surprise, significance, and valence
10. **Update appearance** — project current state outward

### Beliefs and Learning

Beliefs are derived from an agent's frame-set — not stored conclusions but continuous functions over accumulated experience. Each tick, active memories nudge matching belief frames:

```
dispositionDelta(atom) = memory.Value[atom] × memory.Recency × memory.Significance × learningRate[atom]
```

Belief frames decay slowly without reinforcement. High-significance memories decay slowly and contribute strongly to disposition over time. The result: character is formed by accumulated experience, and fades without reinforcement — but slowly enough to feel permanent in practice.

### Memory

Memories encode what an agent expected, what actually happened, and how significant the experience was:

- **Surprise** = `actual - expected` (signed — positive is pleasure, negative is pain)
- **Significance** = `intensity × Max(urgency, |surprise|) × surpriseMagnitude`
- **Decay rate** = inverse of significance — traumatic memories persist, mundane ones fade

### Consent and Capacity

When an agent proposes an interaction with another active agent, the target evaluates the proposal through its own belief function:

- The target scores the proposer's identity context against its urgency profile and disposition frames
- Capacity atoms limit total delivery per tick across all accepted proposals
- Proposals are ranked by score; capacity is allocated greedily from highest to lowest
- A target with a negative disposition toward a proposer may refuse entirely despite available capacity

---

## Examples

### A Hungry Agent

```csharp
var bob = new Agent("bob")
    .WithFrame(new Frame(
        new AtomSet(new Atom("state", 1f)),
        new AtomSet(new Atom("hunger", 0.3f))))
    .WithFrame(new Frame(
        new AtomSet(new Atom("preference", 1f)),
        new AtomSet(new Atom("hunger", 1.0f))))
    .WithFrame(new Frame(
        new AtomSet(new Atom("urgency", 1f)),
        new AtomSet(new Atom("hunger", 2.0f))))   // convex curve — larger deltas feel urgent
    .WithFrame(new Frame(
        new AtomSet(new Atom("decay", 1f)),
        new AtomSet(new Atom("hunger", -0.1f)))); // hunger depletes each tick

var apple = new Agent("apple");
var appleAd = new Advertisement(
    Source: apple,
    Offered: new AtomSet(new Atom("hunger", 0.4f)), // max transfer rate per tick
    SourceAppearance: new AtomSet(new Atom("apple", 1.0f)),
    RequiresConsent: false);

var env = new TestEnvironment()
    .WithStaticAdvertisements("bob", appleAd);

var loop = new SimulationLoop(env, [bob, apple]);
loop.Step(); // Bob eats the apple — hunger increases toward preference
```

### Belief Frames and Relationships

```csharp
// Bob has a learned association: trust and warmth predict social value
var learnedAssociation = new Frame(
    context: new AtomSet(new Atom("trust", 0.3f), new Atom("warmth", 0.5f)),
    value:   new AtomSet(new Atom("social", 0.8f)));

// Bob is currently angry at Alice — fast-decaying modifier
var angerModifier = new Modifier(
    Frame: new Frame(
        context: new AtomSet(new Atom("alice", 1.0f)),
        value:   new AtomSet(new Atom("social", -0.6f))),
    Decay: new Frame(
        context: new AtomSet(new Atom("decay", 1f)),
        value:   new AtomSet(new Atom("social", 0.3f)))); // decays in ~2 ticks

// Alice appears trustworthy and warm — the learned association amplifies her score
// The anger modifier suppresses it — net effect depends on relative magnitudes
// As the anger decays, Alice's score recovers — no explicit reconciliation mechanic needed
```

### Signed Urgency and Avoidance

```csharp
// Alice has too much novelty — she actively avoids novel stimuli
// novelty state 0.8, preference 0.2 → delta -0.6 → urgency = sign(-0.6) × 0.6² = -0.36
var aliceUrgency = UrgencyFunction.Compute(delta: 0.2f - 0.8f, exponent: 2.0f);
// aliceUrgency = -0.36 — negative means avoidance

// When scoring a novel stranger's advertisement, the negative novelty urgency
// suppresses atoms the stranger offers on the novelty axis
```

### Traumatic Memory

```csharp
var traumaticMemory = Memory.Form(
    frame: new Frame(
        context: new AtomSet(new Atom("jimmy", 1.0f)), // context: Jimmy did this
        value:   new AtomSet(new Atom("violent", 0.7f))),
    expected: new AtomSet(new Atom("safety", 0.88f)),  // Bob felt safe
    actual:   new AtomSet(new Atom("safety", 0.28f), new Atom("pain", 0.7f)), // reality
    urgencyProfile: new AtomSet(new Atom("safety", 0.72f), new Atom("pain", 0.49f)));

// High significance → slow decay → persistent learning toward Jimmy disposition
// Over many ticks: Bob develops a stable belief frame { context: jimmy → value: violent }
// Counter-learning requires repeated non-violent Jimmy encounters to erode the disposition
```

---

## Project Structure

```
Animus/
├── Animus.sln
├── Animus/                         # Core engine — no Unity or external dependencies
│   ├── Primitives/
│   │   ├── Atom.cs                 # (string, float) — the universal semantic primitive
│   │   ├── AtomSet.cs              # Collection of atoms
│   │   └── Frame.cs               # (context: AtomSet, value: AtomSet, default?: float)
│   ├── Engine/
│   │   ├── CollectionRoles.cs      # Well-known collection role label constants
│   │   ├── SimilarityFunction.cs   # Compute (strict) and Score (loose) matching
│   │   ├── UrgencyFunction.cs      # Signed Gravity Axiom implementation
│   │   ├── BeliefFunction.cs       # Frame chaining and advertisement scoring
│   │   ├── Observation.cs          # Recursive subject/interaction/object tree
│   │   ├── Advertisement.cs        # Offer from an agent to others
│   │   ├── Intent.cs               # Declared action + ConsentResult + Resolution
│   │   ├── IEnvironment.cs         # Environment boundary interface
│   │   └── SimulationLoop.cs       # Four-phase tick implementation
│   └── Agents/
│       ├── Agent.cs                # Frame-set management and well-known collections
│       ├── Modifier.cs             # Frame + decay frame — transient state reshaping
│       └── Memory.cs               # Frame + significance + recency — learning substrate
└── Animus.Tests/                   # MSTest test suite
    ├── TestEnvironment.cs          # Injectable IEnvironment for integration tests
    ├── Scenario1_TheHungryAgent.cs         # Unit: decay, urgency, scoring, memory
    ├── Scenario2_TwoNeedsOneTick.cs        # Unit: competing urgency resolution
    ├── Scenario3_TheFamiliarFace.cs        # Unit: belief frame chaining, modifiers
    ├── Scenario4_TheTavern.cs              # Unit: multi-agent urgency and scoring
    ├── Scenario5_TheRivalry.cs             # Unit: arbitration, belief suppression
    ├── Scenario6_TheTraumaticEvent.cs      # Unit: trauma, compassion, disposition drift
    ├── Scenario7_TheStranger.cs            # Unit: signed urgency, novel atom discovery
    ├── Integration_Scenario1_TheHungryAgent.cs   # Integration: full loop, apple eating
    ├── Integration_Scenario4_TheTavern.cs        # Integration: consent, capacity splitting
    ├── Integration_Scenario5_TheRivalry.cs       # Integration: arbitration, belief erosion
    ├── Integration_Scenario6_TheTraumaticEvent.cs # Integration: fight-or-flight, trauma
    └── Integration_Scenario7_TheStranger.cs      # Integration: atom discovery, gossip
```

---

## Getting Started

### Requirements

- .NET 8.0 SDK or later
- Any IDE with MSTest support (Visual Studio, Rider, VS Code)

### Build and Test

```bash
git clone https://github.com/your-org/animus
cd animus
dotnet restore
dotnet build
dotnet test
```

All tests should pass. No external dependencies beyond the .NET SDK.

### Run the Demo

`Program.cs` contains **The Tavern with History** — a scenario illustrating every implemented feature across 15 ticks:

```bash
cd Animus
dotnet run
```

You will see six agents — Bob, Alice, Jimmy, the Barkeep, a Stranger, and a Food Platter — interacting over 15 ticks. The output shows tick-by-tick state evolution, resolution outcomes, and a final summary of learned beliefs, active modifiers, and novel atoms discovered.

What to look for:
- **Tick 1** — Bob visits the Stranger immediately (novelty-starved). Novel atoms `tales` and `spice` enter his vocabulary.
- **Tick 2** — Capacity splitting: Jimmy gets partial Barkeep service (rate=0.50) while Bob takes priority.
- **Ticks 1-11** — Bob oscillates between Stranger, Barkeep, and Food Platter as his dominant urgency shifts each tick.
- **Tick 6** — Bob visits the Stranger again. His `novelty` state reaches preference; `tales` and `spice` accumulate in state.
- **Ticks 12+** — Barkeep oversaturates on social and begins refusing everyone via signed urgency avoidance.
- **Final summary** — Bob carries `tales:1.000` and `spice:0.600` in state. His beliefs about Jimmy have drifted via Axiom 5 learning. The trauma modifier is still active and slowly decaying.

### Using the Engine in Your Own Project

The engine requires an `IEnvironment` implementation that provides each agent's perception tree and available advertisements each tick. A `TestEnvironment` is included for testing; implement `IEnvironment` for your own simulation context.

```csharp
var env  = new YourEnvironment(yourWorld);
var loop = new SimulationLoop(env, agents);

while (true)
{
    loop.Step();
    // render, log, or observe loop.Agents
}
```

---

## Implemented Features

| Feature | Status |
|---|---|
| Atom, AtomSet, Frame primitives | ✅ |
| Frame.Default — fallback magnitude for absent labels | ✅ |
| Similarity function — strict (Compute) and loose (Score) modes | ✅ |
| Urgency function — signed Gravity Axiom with configurable exponent | ✅ |
| Belief function — frame chaining, disposition matching, Default support | ✅ |
| Modifier — frame + decay frame, stateless, expiry | ✅ |
| Memory — formation, surprise delta, significance, valence, decay | ✅ |
| Agent — frame-set management, well-known collection accessors | ✅ |
| Observation — recursive subject/interaction/object tree | ✅ |
| SimulationLoop — four-phase tick (Read/Compute, Declare, Resolve, Integrate) | ✅ |
| IEnvironment — clean boundary with god's-eye view | ✅ |
| Axiom 5 — continuous learning from memories into dispositions | ✅ |
| Belief frame decay — dispositions fade without reinforcement | ✅ |
| Per-atom learning rates | ✅ |
| Consent evaluation — disposition-aware, capacity-splitting | ✅ |
| Urgency-weighted arbitration for passive objects | ✅ |
| Signed urgency — approach and avoidance | ✅ |
| Novel atom discovery via absent priors | ✅ |
| Atom propagation via appearance | ✅ |
| Gossip as indirect trust-mediated memory | ✅ |
| Fight-or-flight via urgency scoring | ✅ |
| Compassion as disposition | ✅ |

---

## Roadmap

### Near Term

**Richer consent evaluation** — partial consent at reduced rate, capacity reservation across ticks, multi-tick interaction duration.

**Axiom 5 transformation** — explicit memory consolidation events (e.g. sleep) that accelerate disposition formation from high-significance memories.

### Medium Term

**Spatial environment** — a reference `IEnvironment` implementation with spatial coordinates, signal attenuation by distance and obstacles, and proximity-based advertisement reach. The engine itself remains spatial-agnostic.

**Signal channels** — olfactory, aural, visual, and social channels as atoms; receptor atoms on agents; cross-channel coupling via dispositions (e.g. smell/taste overlap).

**Inventory and ownership** — `("carried", x)` and `("owned", x)` collections with float degree-of-ownership semantics, reservation frames, and transfer mechanics.

### Later

**Sub-engines and plugins** — the frame-based agent structure supports specialized evaluators that operate on agent frames the core engine ignores. A combat sub-engine, narrative sub-engine, or social graph engine can coexist without core engine changes.

**Reasoning, prediction, and planning** — agents modeling other agents' belief frames and running lightweight internal simulations to anticipate outcomes. The most ambitious extension, likely requiring a new layer above the core loop.

**Spatial reasoning** — agents navigating toward advertisements, path planning, territory and proximity as atoms.

---

## Design Notes

The design evolved through extensive scenario-driven validation. Seven canonical scenarios — from a single hungry agent to a traumatic event witnessed by bystanders — were used to stress-test the model before any code was written, and then again as executable integration tests. Several apparent gaps in the model turned out to already be handled by existing machinery, validating the Closed Algorithm principle.

Notable emergent behaviors validated by the test suite:

- **Personality** emerges from disposition configuration, not a personality system
- **Trust** is an observer label for behavioral patterns — not stored, just expressed
- **Fear** is low safety state — no separate fear atom or mechanism needed
- **Altruism** emerges from preference structure — no special altruism flag
- **Habituation** is implicit — repeated uneventful experiences form weaker memories as expectations calibrate
- **Character arc** is disposition drift over simulation time — automatic and free

Notable emergent behaviors observed in The Tavern with History demo:

- **Novelty seeking** — Bob visits the Stranger immediately when novelty-starved, without any explicit novelty-seeking behavior programmed
- **Capacity splitting** — the Barkeep naturally prioritizes Alice (liked) over Jimmy (disliked) and splits remaining capacity proportionally
- **Signed urgency avoidance** — the Barkeep starts refusing all social interactions once his social state overshoots preference
- **Novel atom accumulation** — `tales` and `spice` propagate from Stranger into Bob's state and persist across ticks
- **Belief drift** — Bob's negative belief about Jimmy erodes slightly over 15 ticks via Axiom 5 learning, even without direct counter-evidence
- **Trauma persistence** — the safety modifier from Jimmy's past violence is still active at tick 15, decaying at 0.02f/tick

---

## License

MIT