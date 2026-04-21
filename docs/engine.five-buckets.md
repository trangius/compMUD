# The five buckets

Every concept in the engine fits exactly one of five shapes: **Entity**,
**State**, **Behavior**, **Effect**, or **Category**. Pick one per new
concept; don't smoosh.

---

## 1. Entities — world objects

- Have [`Position`](../Engine/Spatial/Spatial.cs#L9), participate in ticks, can be destroyed.
- Examples: rabbit, wolf, corpse, bush, tree, grass, wall, arrow-in-flight,
  wolf-raid-spawner (an entity with no position but still a live integer id).
- **Pattern**: `int id` + components. Built by an archetype `Create*` function
  in [`Archetypes.cs`](../Engine/Archetypes.cs).

---

## 2. States — properties of a specific entity

- Marker components (often empty classes) attached to one entity. If the state
  carries information, add fields; otherwise leave empty.
- Examples: [`Walkable`](../Engine/Spatial/Spatial.cs#L25), [`Solid`](../Engine/Spatial/Spatial.cs#L29), [`Corpse`](../Engine/Stats/Health.cs#L24), [`Tree`](../Engine/Tree.cs#L5), [`RaidingWolf`](../Engine/Behaviors/WolfRaid.cs#L6), [`Stats`](../Engine/Stats/Stats.cs#L13),
  [`Melee`](../Engine/Behaviors/Hunt.cs#L29), [`Grappled`](../Engine/Behaviors/Grapple.cs#L8).
- **Pattern**: `public class Sleeping { }` or with fields. Checked via
  [`World.HasComponent<T>(id)`](../Engine/World.cs#L220), read via [`World.GetComponent<T>(id)`](../Engine/World.cs#L228).

---

## 3. Behaviors — active logic per tick: *pick one*

- [`IBehavior`](../Engine/Behaviors/Behavior.cs#L9) implementations. An entity's [`Behaviors`](../Engine/Behaviors/Behavior.cs#L28) component holds a list
  of them. The dispatcher asks each [`WouldAct`](../Engine/Behaviors/Behavior.cs#L17), runs only the highest-priority
  winner's [`Act`](../Engine/Behaviors/Behavior.cs#L24). One action per entity per tick.
- Examples: [`EscapeGrappleBehavior`](../Engine/Behaviors/Grapple.cs#L53), [`RunFromPredatorBehavior`](../Engine/Behaviors/RunFromPredator.cs#L9),
  [`HuntBehavior`](../Engine/Behaviors/Hunt.cs#L60), [`FeedBehavior`](../Engine/Behaviors/Feeding.cs#L43), [`BreedBehavior`](../Engine/Behaviors/Breeding.cs#L15), [`RestBehavior`](../Engine/Behaviors/Rest.cs#L6),
  [`WanderBehavior`](../Engine/Behaviors/Wander.cs#L4), [`ReturnToForestBehavior`](../Engine/Behaviors/WolfRaid.cs#L17); future `AttackBehavior`,
  `CastSpellBehavior`.
- **Pattern**:
  ```csharp
  public class SomeBehavior : IBehavior
  {
      public int Priority => priorityValue;       // higher = more important
      public bool WouldAct(int id) { ... }        // also caches target info
      public int Act(int id)       { ... }        // uses cached info, returns cost
  }
  ```
  `Act` returns the action's cost as an integer multiplier of the entity's
  period. A step is the baseline; slower actions (bite, cast, mate) return
  larger multipliers. See `engine.scheduler.md`.
- **Priorities ordering (rabbit example, higher wins):**
  escape-grapple and flee reflexes at the top, then eating, then breeding,
  then rest, with wander as a priority-0 fallback. Specific numbers live
  in each behavior's `Priority` — check the source when tuning.

---

## 4. Effects — passive per-tick updates: *run all*

- [`IEffect`](../Engine/Effects/Effect.cs#L10) implementations, in an entity's [`Effects`](../Engine/Effects/Effect.cs#L16) component list. Every
  effect runs every tick; no competition, no priority. Wall-clock — fires
  regardless of the entity's pace.
- Use for drain / decay / regen / status / raid spawning — anything that
  happens *to* the entity automatically rather than being a decision it makes.
- Examples: [`EnergyDrainEffect`](../Engine/Stats/Energy.cs#L31), [`WolfRaidEffect`](../Engine/Behaviors/WolfRaid.cs#L97); future `Poisoned`,
  `Burning`, `Aging`, cooldown tickers, mana regen.
- **Pattern**: `public class SomeEffect : IEffect { public void Apply(int id) { ... } }`.

---

## 5. Categories — shared abstract labels

- Singleton instances of a category class. No position, no lifecycle. Many
  entities point at the same instance.
- Examples: [`Resources.Meat`](../Engine/Yields.cs#L23), [`Resources.Berry`](../Engine/Yields.cs#L24), [`Resources.Pelt`](../Engine/Yields.cs#L25),
  [`Resources.Bone`](../Engine/Yields.cs#L26); future `Materials.Steel`, `DamageTypes.Fire`,
  `Rarities.Uncommon`.
- **Pattern**:
  ```csharp
  public class ResourceCategory { public readonly string name; ... }
  public static class Resources
  {
      public static readonly ResourceCategory Meat = new("meat");
      public static readonly ResourceCategory Berry = new("berry");
      public static readonly ResourceCategory Pelt = new("pelt");
      public static readonly ResourceCategory Bone = new("bone");
  }
  ```
- Identity is the object reference (`==` compares pointers). The `name` string
  is for display only — never for identity logic.

---

## Decision tree

When adding a concept:

- Has a position and lives in the world? → **Entity**
- A current property of one specific entity? → **State**
- Active logic that *chooses* what to do this tick? → **Behavior**
- Passive update that *always happens* each tick? → **Effect**
- A label many things reference? → **Category**

---

## A worked example

A poisoned rabbit corpse is:
- An **Entity** (the body, a new int id).
- With **States**: `Corpse` (marker), `Walkable`, [`Yields`](../Engine/Yields.cs#L52) `{ [meat, pelt, bones] }`.
- With an **Effect**: `Poisoned` that ticks down the entity's HP.
- Referencing a **Category**: each [`Yield.category`](../Engine/Yields.cs#L35) points at `Resources.Meat`,
  `Resources.Pelt`, or `Resources.Bone`.
- No **Behavior** — it's not choosing anything.

One concept, five slots, placed unambiguously.
