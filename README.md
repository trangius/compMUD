# Composition MUD Engine

A small C# engine where every game object is an integer ID with attached components. Behaviors live on the objects themselves — no external systems loop over entities.

Work in progress toward a rougelike with creatures, humans, NPCs, items, shops, and dialogue, all built from composed components.

## Running

Two frontends share one engine:

```bash
dotnet run --project Console   # text-grid frontend with interactive commands
dotnet run --project Game      # MonoGame graphical frontend
```

Both call `World.Initialize(w, h)` to set dimensions, then an area builder like `HomeArea.StartingArea()` to populate the world, then `World.Tick()` in a loop. `World` holds the domain-agnostic engine API; each file in `Engine/Areas/` is one concrete area.

## Architecture

### The three stores in `World`

- **`entities`** — `HashSet<int>`. An entity *is* an int; this says which ints exist.
- **`components`** — `Dictionary<Type, Dictionary<int, object>>`. Outer key = component type, inner key = entity id. `components[typeof(Position)][42]` is entity 42's Position. A missing inner key means the entity doesn't have that component.
- **`spatialIndex`** — `Dictionary<(int, int), List<int>>`. Reverse lookup from cell to the entity ids at that cell. Makes "who's at (5, 10)?" instant instead of scanning every Position.

### The tick

Two dispatches, in order:

**1. Actions — pick one.** For every entity with a `Behaviors` component:
1. Ask each `IBehavior.WouldAct(id)` — each behavior reads world state and caches what it would do (e.g. which threat to flee from).
2. Pick the highest-priority behavior that returned true.
3. Run only that one's `Act(id)`. One action per entity per tick, structurally.

**2. Effects — run all.** For every entity with an `Effects` component, every effect in its list runs its `Apply(id)`. No competition, no priority — passive per-tick updates. Energy drain is the first one; future status effects (poison, burning, aging) slot into the same list.

### The five buckets

When adding a new concept, it fits one of five slots (full details in `CLAUDE.md`):

- **Entities** — world objects with `Position`. Built by `Archetypes.Create*`.
- **States** — marker components on one specific entity. `Walkable`, `Solid`, `Corpse`.
- **Behaviors** (pick one) — active logic per tick. `IBehavior` with `Priority`, `WouldAct`, `Act`.
- **Effects** (run all) — passive per-tick updates. `IEffect` with `Apply`. Drain, decay, regen, status.
- **Categories** — singleton labels many entities reference. `Resources.Meat`, `Resources.Berry`.

## Per-entity pacing (in flight)

The current tick is global: every entity acts once per `World.Tick()`. That's a
stepping stone. The game design is **each entity has its own pace** — a hummingbird
acts many times for every one action of a tree, and spells like Haste or Slow
are just temporary changes to an entity's pace.

Shape of the change:

- **Scheduler component** on every actor: `{ period, nextActTick }`. `period` is
  the base ticks-between-actions. `period = 1` is the fastest an entity can go;
  larger means slower. No upper bound, so a tree might have `period = 10000` and
  still tick. Archetypes set the species default (wolf fast, rabbit slower, tree
  glacial).
- **World.Tick becomes thinner.** It advances a global clock. An entity acts
  only when `tickCount ≥ nextActTick`. After acting, `nextActTick` is pushed
  forward by the entity's `period`. The world doesn't decide who ticks; entities
  decide when they're next due.
- **Effects stay wall-clock.** Poison, energy drain, aging — these fire every
  global tick on every entity regardless of pace. A poisoned slow creature and
  a poisoned fast creature both die in the same real-time window; the slow one
  simply gets fewer actions during the dying.
- **Later: varied action cost.** `Act()` returns a cost. A step might cost 1,
  a bite 2, a long incantation 5. `nextActTick = now + period × cost`. Lands as
  a second change once speed is in and tuned — don't want to tune eight knobs
  at once with no prior feel.

Players and NPCs share the same scheduler. "Outside of time" effects (one
entity at period 1 while everything else sits at period 1000) fall out of the
model for free.

## The spatial index: four paths in, no side doors

`spatialIndex` is maintained by exactly four public methods on `World`. Nothing else writes to it.

| Method | Effect on the index |
|---|---|
| `AttachComponent(id, new Position(x, y))` | Adds entity at `(x, y)`. If already placed, removes from old cell first. |
| `MoveEntity(id, newX, newY)` | Removes from old cell, writes new Position, adds to new cell. |
| `DetachComponent<Position>(id)` | Takes the entity off the map (the entity still exists). |
| `DestroyEntity(id)` | If positioned, removes from the index; then wipes all components. |

Gotcha: never write directly to `components[typeof(Position)][id]`. Only `MoveEntity` does that *with* the index update. Bypassing it leaves the index stale and subsequent spatial queries lie.

## File layout

```
Engine/
  World.cs                          state, tick, queries (domain-agnostic)
  Archetypes.cs                     "what is a rabbit" — entity factories
  EntityInfo.cs                           Named, Appearance — display/label metadata
  Areas/
    HomeArea.cs                     starting area — walls, pond, trees, creatures
  Spatial/
    Spatial.cs                      Position, Walkable, Solid + MovementHelper
    Sensing.cs                      Sensing (vision range)
  Stats/
    Health.cs                       Health, Corpse marker, DeathHelper
    Energy.cs                       Energy, EnergyDrainEffect
  Behaviors/
    Behavior.cs                     IBehavior + Behaviors component (pick one)
    Hunt.cs                         Hunts, Attacking, HuntBehavior
    Flee.cs                         Flees, FleeBehavior
    Wander.cs                       WanderBehavior
    Feeding.cs                      Drops, ResourceItem, Diet, ResourceCategory,
                                    Resources, HarvestBehavior, FeedBehavior
    Breeding.cs                     Breeding, BreedBehavior
    Vegetation.cs                   Vegetation, GrowBehavior
  Effects/
    Effect.cs                       IEffect + Effects component (run all)

Console/Program.cs                  text-grid frontend
Game/Game1.cs                       MonoGame graphical frontend
CLAUDE.md                           architecture rules + five-bucket taxonomy
```
