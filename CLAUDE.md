# Claude Project Instructions

## Logic Direction: Check for the Positive
Never write logic that lists things that block or prevent an action. Instead, check for the thing that permits it. For example, movement should check "is there Walkable ground?" — not "is there Solid? is there Liquid? is there Lava?" The negative list grows forever. The positive check stays one line.

## Architecture: Composition over Inheritance
Prefer composition and interfaces over deep inheritance hierarchies. Behaviors should be encapsulated in small, focused classes that implement interfaces, then composed onto game objects.

```csharp
// Preferred: composition
class Player
{
    public IMovement movement;
    public IAttack attack;
    public IPolarity polarity;
}

// Avoid: deep inheritance
class Player : Character : Entity : GameObject ...
```

Interfaces describe what something *can do*, not what it *is*. A class should implement only the interfaces relevant to its behavior.

## Engine Structure

Frontends (Console, Game) call three things: `World.Initialize(w, h)` to set dimensions, an area builder (e.g. `HomeArea.StartingArea()`) to populate the world, and `World.Tick()` in a loop.

```
Engine/
  World.cs           # state + Initialize() + Tick() + queries (domain-agnostic)
  Archetypes.cs      # entity factories — "what is a rabbit"
  EntityInfo.cs            # Named, Appearance — display/label metadata
  Areas/             # one file per area — HomeArea, future Dungeon, Market, ...
  Spatial/           # Position, Walkable, Solid + MovementHelper; Sensing
  Stats/             # Health, Corpse, DeathHelper; Energy, EnergyDrainEffect
  Behaviors/         # IBehavior + Behaviors (pick one); Hunt, Flee, Wander, Feeding, Breeding, Vegetation
  Effects/           # IEffect + Effects (run all); future Poisoned, Burning, Aging...
```

When you are asked to run and test the program, do so with the Console frontend. You can read output, do input, and skip one tick at a time to see exactly what happens.

## Design: Composition, not ECS

This is NOT an Entity-Component-System engine, even though the World holds entities and components. The difference matters:

- **Components carry behavior.** Health has TakeDamage(), Energy has Drain(). Never dumb data bags that external systems manipulate.
- **Behaviors act directly.** FleeBehavior moves the entity via MovementHelper. No intents, no middlemen, no deferred resolution queue.
- **Entities pick ONE action per tick.** Each entity has a Behaviors list of IBehavior implementations. World.Tick asks each WouldAct, then runs Act on the highest-priority willing behavior — exactly one action per entity per tick.
- **Component + its behavior live in the same file.** Hunt.cs has the marker (Hunts), the component (Attacking), AND the behavior (HuntBehavior) together. One file, one feature.
- **Single-instance classes are static.** One World → static class. No `new World()`, no passing `world` around.
- **Entities are integer IDs** with components in World's dictionaries. This enables dynamic composition — pick up a sword, gain Attacking at runtime.
- **One source of truth.** Don't expose derived booleans (IsHungry, IsStarving) that can disagree with the underlying data. Let callers compute from the value.
- **No data-only classes.** If a class only holds fields with no methods, push logic onto it. (Categories are the exception — see below.)

## Design: Five buckets

When adding a new concept, decide which bucket it fits and use the matching pattern. Don't smoosh.

**1. Entities** — world objects
- Have `Position`, participate in ticks, can be destroyed.
- Examples: rabbit, wolf, corpse, skeleton, bush, tree, grass, wall, sword, door, arrow-in-flight.
- Pattern: `int id` + components. Built by an archetype `Create*` in `Archetypes.cs`.

**2. States** — current properties OF a specific entity
- Marker components (often empty classes) attached to one entity. Add data fields if the state carries them.
- Examples: `Walkable`, `Solid`, `Hunts`, `Flees`, `Corpse`; future `Sleeping`, `Locked`, `Equipped`.
- Pattern: `public class Sleeping { }`. Checked via `World.HasComponent<T>(id)`.

**3. Behaviors** — active logic per tick: *pick one*
- `IBehavior` implementations, dispatched by priority from an entity's `Behaviors` component.
- The dispatcher asks each `WouldAct`, runs only the highest-priority winner's `Act`. One action per entity per tick.
- Examples: `FleeBehavior`, `HuntBehavior`, `WanderBehavior`; future `AttackBehavior`, `CastSpellBehavior`.
- Pattern: class with `Priority`, `WouldAct`, `Act`.

**4. Effects** — passive per-tick updates: *run all*
- `IEffect` implementations, in an entity's `Effects` component list. Every effect runs every tick; no competition, no priority.
- Use for drain/decay/regen/status — anything that happens *to* the entity automatically rather than being a decision.
- Examples: `EnergyDrainEffect`; future `Poisoned`, `Burning`, `Aging`, `Decomposition`, cooldown tickers, mana regen.
- Pattern: class with `Apply(int id)`.

**5. Categories** — shared abstract labels many entities reference
- Singleton instances of a category class. No position, no lifecycle. Many entities point at the same instance.
- Examples: `Resources.Meat`, `Resources.Berry`; future `Materials.Steel`, `Rarities.Uncommon`, `DamageTypes.Fire`.
- Pattern:
  ```csharp
  public class ResourceCategory { public readonly string name; ... }  // the class
  public static class Resources {                                  // the registry
      public static readonly ResourceCategory Meat = new("meat");
  }
  ```
  Identity is the object reference (`==` compares pointers). The `name` string is for display only — never for identity logic.

**When adding a concept, ask:**
- Has a position and lives in the world? → **Entity**
- A current property of one thing? → **State**
- Active logic that *chooses* what to do this tick? → **Behavior**
- Passive update that *always happens* each tick? → **Effect**
- A label many things reference? → **Category**

A poisoned rabbit corpse is an **Entity** (the body) with **States** (`Corpse`), an **Effect** (`Poisoned` draining its own hit points), and a **Category reference** (`ResourceItem.resourceType = Resources.Meat`).

## Code Style
- Follow standard C# naming convention
- All variables (public and private) use camelCase, never PascalCase. PascalCase is only for types, methods, and interfaces.
- Prefer explicit types over `var` unless the type is obvious from context

## General Principles
- Characters use circle collision, walls and tiles use rectangle collision
- Do not refactor large systems unless explicitly asked.

## Comments
- Comments are navigation — you should understand a file by reading only the comments.
- Never delete comments without asking. If wrong, update it — don't remove it.
- Add a comment before every code block of 4+ lines.
- For functions, use a visual separator. These matter — the user has poor eyesight and relies on them for scanning and for seeing structure in the minimap. Don't remove them even if they feel redundant.
  // ----------------------------------------------------------------------------
  // <what this function does>
  // ----------------------------------------------------------------------------
- Language: short, concrete, narrating. Like telling someone what happens next.
  Good: "Find the nearest food and move toward it"
  Good: "Both parents go on cooldown after mating"
  Bad: "This method searches for the nearest food entity within vision range"
  Bad: "Execute harvesting behavior on the target Drops entity"
- Flag gotchas — things that would trip someone up or break if changed.
- For types in the five buckets, start the class comment with the bucket name as a prefix: `// State: ...`, `// Behavior: ...`, `// Effect: ...`, `// Category: ...`. Makes taxonomy visible at a glance without adding interface plumbing.
