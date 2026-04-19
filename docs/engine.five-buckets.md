# The five buckets

Every concept in the engine fits exactly one of five shapes: **Entity**,
**State**, **Behavior**, **Effect**, or **Category**. Pick one per new
concept; don't smoosh.

---

## 1. Entities — world objects

- Have `Position`, participate in ticks, can be destroyed.
- Examples: rabbit, wolf, corpse, bush, tree, grass, wall, arrow-in-flight,
  wolf-raid-spawner (an entity with no position but still a live integer id).
- **Pattern**: `int id` + components. Built by an archetype `Create*` function
  in `Archetypes.cs`.

---

## 2. States — properties of a specific entity

- Marker components (often empty classes) attached to one entity. If the state
  carries information, add fields; otherwise leave empty.
- Examples: `Walkable`, `Solid`, `Corpse`, `Tree`, `RaidingWolf`, `Stats`,
  `Melee`, `Grappled`.
- **Pattern**: `public class Sleeping { }` or with fields. Checked via
  `World.HasComponent<T>(id)`, read via `World.GetComponent<T>(id)`.

---

## 3. Behaviors — active logic per tick: *pick one*

- `IBehavior` implementations. An entity's `Behaviors` component holds a list
  of them. The dispatcher asks each `WouldAct`, runs only the highest-priority
  winner's `Act`. One action per entity per tick.
- Examples: `EscapeGrappleBehavior`, `RunFromPredatorBehavior`,
  `HuntBehavior`, `FeedBehavior`, `BreedBehavior`, `RestBehavior`,
  `WanderBehavior`, `ReturnToForestBehavior`; future `AttackBehavior`,
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

- `IEffect` implementations, in an entity's `Effects` component list. Every
  effect runs every tick; no competition, no priority. Wall-clock — fires
  regardless of the entity's pace.
- Use for drain / decay / regen / status / raid spawning — anything that
  happens *to* the entity automatically rather than being a decision it makes.
- Examples: `EnergyDrainEffect`, `WolfRaidEffect`; future `Poisoned`,
  `Burning`, `Aging`, cooldown tickers, mana regen.
- **Pattern**: `public class SomeEffect : IEffect { public void Apply(int id) { ... } }`.

---

## 5. Categories — shared abstract labels

- Singleton instances of a category class. No position, no lifecycle. Many
  entities point at the same instance.
- Examples: `Resources.Meat`, `Resources.Berry`; future `Materials.Steel`,
  `DamageTypes.Fire`, `Rarities.Uncommon`.
- **Pattern**:
  ```csharp
  public class ResourceCategory { public readonly string name; ... }
  public static class Resources
  {
      public static readonly ResourceCategory Meat = new("meat");
      public static readonly ResourceCategory Berry = new("berry");
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
- With **States**: `Corpse` (marker), `Walkable`, `ResourceItem { ... meat ... }`.
- With an **Effect**: `Poisoned` that ticks down the entity's HP.
- Referencing a **Category**: `ResourceItem.resourceType = Resources.Meat`.
- No **Behavior** — it's not choosing anything.

One concept, five slots, placed unambiguously.
