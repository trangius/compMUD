# Composition MUD Engine

Private repo. This README is the index. Deep explanations live in `docs/`.

## Doc index

Every entry is: *file — what it covers. Update when: what kind of change
warrants touching it.*

### Top-level

- [docs/projects.md](docs/projects.md) — the three projects (Engine, Console,
  Gui) and how they compose. **Update when:** adding a new frontend or
  top-level project, or changing how frontends invoke the engine.
- [docs/console.readme.md](docs/console.readme.md) — Console frontend commands
  and usage. **Update when:** adding or renaming Console commands, or
  changing sprite glyphs.
- [docs/gui.readme.md](docs/gui.readme.md) — Gui frontend (placeholder).
  **Update when:** any Gui work lands.

### Engine — how the sim thinks

- [docs/engine.composition.md](docs/engine.composition.md) — the composition
  design principles (not ECS). **Update when:** the design rules change
  (e.g. the "one action per tick" rule is revisited).
- [docs/engine.five-buckets.md](docs/engine.five-buckets.md) — the Entity /
  State / Behavior / Effect / Category taxonomy. **Update when:** a new
  bucket is introduced, or the decision tree for picking one changes.
- [docs/engine.tick.md](docs/engine.tick.md) — the two-pass tick dispatcher
  (Behaviors gated by Scheduler, then Effects wall-clock). **Update when:**
  the dispatch order changes or new passes are added.
- [docs/engine.scheduler.md](docs/engine.scheduler.md) — per-entity pacing
  via `AgilityPaced` / `FixedPaced`. **Update when:** scheduler semantics
  change, or the current species pace tuning shifts.
- [docs/engine.stats.md](docs/engine.stats.md) — `Stats` component, scale,
  `StatMath` derived formulas. **Update when:** adding a new stat, changing
  a formula constant, or changing how stats interact with resources.
- [docs/engine.movement.md](docs/engine.movement.md) — BFS, pathfinding, the
  8-connected grid, and the Euclidean-vs-Chebyshev range-shape gotcha.
  **Update when:** adding a new movement helper, changing connectivity, or
  introducing new passability semantics (swim, climb, fly).
- [docs/engine.species.md](docs/engine.species.md) — species identity via
  `Species.spawn` delegate, used for breeding and predation. **Update when:**
  introducing new same-species-matching logic, or changing how `Predator`/
  `Breeding` key off identity.
- [docs/engine.spatial-index.md](docs/engine.spatial-index.md) — the "four
  paths in, no side doors" rule for `spatialIndex`. **Update when:** the
  allowed write paths change (adding a new one, or discovering a violation).

### Engine — how to extend it

- [docs/engine.filestructure.md](docs/engine.filestructure.md) — the Engine
  folder tree and the file-per-feature principle. **Update when:** adding,
  moving, renaming, or splitting files in `Engine/`.
- [docs/engine.add-entity.md](docs/engine.add-entity.md) — step-by-step for
  adding a new creature / object. **Update when:** the archetype pattern
  changes (new required component, new default behavior), or a step in the
  recipe becomes outdated.
- [docs/engine.examplerun.md](docs/engine.examplerun.md) — what the CPU
  actually does from `Program.cs` through a few ticks. **Update when:**
  `World.Tick` internals change, or when the archetype startup sequence
  changes meaningfully enough that the trace becomes inaccurate.

### Rules for Claude

- [CLAUDE.md](CLAUDE.md) — imperative rules and style guides. **Update when:**
  a new rule crystallizes from feedback, or an old one is retired.

## Running

```bash
dotnet run --project Console   # text frontend — see docs/console.readme.md
dotnet run --project Game      # GUI frontend  — see docs/gui.readme.md
```
