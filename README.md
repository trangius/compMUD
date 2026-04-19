# Composition MUD Engine

Private repo. This README is the index — plain-English summaries of what each
doc is about. Deep explanations live in `docs/`. Claude's imperative rules
(including when to update each doc) are in [CLAUDE.md](CLAUDE.md).

## How the engine is built

The engine is a C# library. A frontend builds a starting world by
calling archetype functions, then advances time by calling `World.Tick`
in a loop and reads the world's components to render. The engine knows
nothing about screens or input.

### Entities are integer ids with components attached

Every thing in the world — a rabbit, a bush, a wall — is a plain `int`.
There is no `Entity` class and no inheritance tree. A fresh id means
nothing until you attach components to it.

Components are small objects with methods. `Health` has `TakeDamage`.
`Energy` has `Drain` and `Restore`. `Position` has `X` and `Y`. The
static `World` stores each component in a dictionary keyed by type and
id. You read them with `World.HasComponent<T>(id)` and
`World.GetComponent<T>(id)`.

There is no class called `Rabbit`. There is a function `CreateRabbit`
in `Archetypes.cs` that attaches the right components to a fresh id:
`Stats`, `Health`, `Energy`, `Position`, `Species`, a `Scheduler`, a
`Behaviors` list, an `Effects` list. Change the list of components and
you change what the entity is. A creature can gain an `Attacking`
component at runtime, or lose its `Flees` component, without replacing
the object.

### Every concept fits one of five slots

Every new idea belongs to exactly one of five categories:

- **Entity** — a thing in the world with a position.
- **State** — a property of one entity (`Walkable`, `Corpse`, `Melee`).
- **Behavior** — a decision the entity makes on its turn (flee, hunt,
  eat).
- **Effect** — something that happens to the entity every tick whether
  it chose it or not (energy drain, poison, aging).
- **Category** — a shared label many entities point at (one
  `Resources.Meat` instance referenced by every piece of meat).

Pick one per concept; don't blur them.

### A tick is two passes

`World.Tick` is the only way time advances. Two passes in order, then
the tick counter increments.

**Pass one, actions.** For every entity with a `Behaviors` list, the
dispatcher asks the scheduler whether the entity is due this tick. If
so, it asks each behavior `WouldAct`; the highest-priority one that
said yes runs its `Act`. Exactly one action per entity per tick.

**Pass two, effects.** For every entity with an `Effects` list, every
effect runs. No priority, no competition. Drains, decays, and regens
fire regardless of what the entity chose — which is why a creature
cannot escape starvation by choosing not to eat.

### The scheduler paces each entity

Not every entity acts every tick. Each one carries a `Scheduler` that
records the tick on which its next turn is due; the dispatcher skips it
until then. For statted creatures the period is derived from `Agility`.
For simpler things (a bush) the period is a literal number.

Actions cost time in periods. A step costs 1, a bite costs 3, mating
costs 8. After acting, the scheduler pushes the next turn forward by
`period × cost`.

### Components keep themselves in sync

To answer "who is on cell (5, 7)?" without scanning every entity, the
world keeps a reverse map from cell to ids. `Position` maintains that
map itself: its `OnAttach` hook inserts, its `OnDetach` hook removes.
No manager class, no system that has to remember to update anything.

The pattern generalises. A component that needs work at attach or
detach time implements `IOnAttach` or `IOnDetach`, and the component
store calls the hook. `World` knows nothing about what individual
components do when they come and go.

### What this gives you in practice

One file per feature. `Hunt.cs` holds the `Predator` marker, the
`Attacking` companion state, and the `HuntBehavior` together. To add a
new creature, write one archetype function. To add a new ability — a
spell, a disease, a weather effect — write a state marker plus a
behavior or an effect. There is no central switch statement to edit.

## Doc index

The docs below are listed in a **suggested reading order**. Read top to
bottom for the intended flow, or jump to whichever doc answers your
current question. Each entry says what you'll find inside.

### Orient

- [docs/projects.md](docs/projects.md) — The three .NET projects
  (`Engine/`, `Console/`, `Gui/`) and the three calls every frontend
  performs: `World.Initialize`, an area builder, `World.Tick`.

### The model

- [docs/engine.composition.md](docs/engine.composition.md) — The
  composition rules in full: components carry behavior, entities are
  integer ids with components attached dynamically, `World` is static,
  no data-only classes. Where logic lives for any given concern, and
  where it definitely doesn't.

- [docs/engine.tick.md](docs/engine.tick.md) — `World.Tick` spelled out
  step by step: the scheduler gate, the `WouldAct` / `Act` competition
  that picks one action per entity, the effects pass that runs on
  everyone, and what happens when entities are created or destroyed
  mid-tick.

### See it run

- [docs/engine.examplerun.md](docs/engine.examplerun.md) — A
  method-by-method trace of startup, a rabbit's first tick, and a
  later breeding tick. Every name you've read so far now points at
  real code being called.

### The taxonomy, named

- [docs/engine.five-buckets.md](docs/engine.five-buckets.md) — The
  five categories every concept in the engine fits into (Entity,
  State, Behavior, Effect, Category), a decision tree for picking one,
  and a worked example that uses all five at once (a poisoned rabbit
  corpse).

### Topical deep-dives

- [docs/engine.scheduler.md](docs/engine.scheduler.md) — How each
  entity is paced: `AgilityPaced` vs `FixedPaced`, the full
  action-cost table, and why a newly spawned baby waits one full
  period instead of acting on the tick it was born.

- [docs/engine.stats.md](docs/engine.stats.md) — The four base stats
  (Strength, Agility, Perception, Toughness), the `StatMath` derived
  formulas, the stat-vs-resource distinction, and when a new stat is
  worth adding.

- [docs/engine.movement.md](docs/engine.movement.md) — 8-connected
  movement helpers (`TryMove`, `MoveToward`, `MoveAwayFrom`,
  `Wander`), generic BFS pathfinding, and the trap that "in vision"
  (Euclidean disk) is not the same as "reachable" (Chebyshev / BFS).

- [docs/engine.species.md](docs/engine.species.md) — Species identity
  is the archetype's spawn delegate — no enum, no registry. How
  breeding, hunting, and population caps all use reference equality on
  that delegate.

- [docs/engine.spatial-index.md](docs/engine.spatial-index.md) — How
  `Position` keeps the cell-to-ids reverse map in sync via `IOnAttach`
  / `IOnDetach`. The four legitimate placement methods, why writing
  `components[typeof(Position)]` directly corrupts the index silently,
  and the read-only query surface.

### Extending the engine

- [docs/engine.filestructure.md](docs/engine.filestructure.md) —
  Folder-by-folder map of `Engine/`. The file-per-feature rule (a
  marker, its companion state, and the behavior that reads them share
  one file) and its exceptions (cross-cutting components like
  `Species`, `Position`, and the schedulers `AgilityPaced` /
  `FixedPaced`).

- [docs/engine.add-entity.md](docs/engine.add-entity.md) —
  Step-by-step recipe for adding a new creature: bucket decisions, new
  files, archetype method, area wiring. Ends with a hawk example and
  the usual common mistakes.

### Frontends

- [docs/console.readme.md](docs/console.readme.md) — The ASCII
  frontend and its REPL commands (`look`, `tick`, `info`, `status`,
  `log`). How to pipe scripted input for automated runs.

- [docs/gui.readme.md](docs/gui.readme.md) — The MonoGame frontend.
  Placeholder; lists what still needs documenting (renderer, input,
  camera, sprite loading).

## Running

```bash
dotnet run --project Console   # text frontend — see docs/console.readme.md
dotnet run --project Gui       # GUI frontend  — see docs/gui.readme.md
```
