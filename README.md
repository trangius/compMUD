# Composition MUD Engine

Private repo. This README is the index — plain-English summaries of what each
doc is about. Deep explanations live in `docs/`. Claude's imperative rules
(including when to update each doc) are in [CLAUDE.md](CLAUDE.md).

## Doc index

### Top-level

- [docs/projects.md](docs/projects.md) — The repo is split into three
  projects: the engine (which runs the simulation), a text frontend, and a
  graphical frontend. The engine knows nothing about screens or input — it
  just ticks. A frontend is a thin shell that builds a world, then calls
  tick in a loop. Adding a new frontend is almost nothing.

- [docs/console.readme.md](docs/console.readme.md) — The text frontend.
  Prints the world as ASCII, takes commands like *look*, *tick*, *info*,
  *log*. It's the fastest way to watch what the sim is doing and poke
  individual cells. You can also pipe a script of commands into it for
  automated runs — that's how most of the gameplay balancing got done.

- [docs/gui.readme.md](docs/gui.readme.md) — The graphical frontend. Same
  engine, prettier pictures. Mostly a placeholder for now with a list of
  things still to write up — rendering, input, camera, that sort of thing.

### Engine — how the sim thinks

- [docs/engine.composition.md](docs/engine.composition.md) — How the
  engine is put together. A creature isn't a subclass of Animal of Thing
  of Object — it's just an id with a bag of small parts stuck to it. Each
  part knows how to do its own job.

- [docs/engine.five-buckets.md](docs/engine.five-buckets.md) — Every new
  thing you add fits into one of five categories:
  - **Entity** — a thing in the world.
  - **State** — a property of a thing.
  - **Behavior** — a decision a thing makes.
  - **Effect** — something that happens to a thing automatically.
  - **Category** — a shared label many things point to.

  Pick one. Don't blur them. The doc has a decision tree and a worked
  example showing all five slots at once.

- [docs/engine.tick.md](docs/engine.tick.md) — What actually happens when
  time moves forward by one step. Each creature picks one thing to do —
  only one, and only the most important one. Then passive stuff (hunger,
  poison, decay) ticks for everyone. A creature can't dodge starvation by
  choosing not to eat, because the two passes are separate.

- [docs/engine.scheduler.md](docs/engine.scheduler.md) — How the engine
  decides *how often* each creature gets a turn. Wolves are quicker than
  rabbits; rabbits are quicker than bushes. Some actions are slow and cost
  extra time — biting takes longer than stepping, mating takes longer
  still. That slowness shifts the creature's next turn further into the
  future, so a wolf mid-kill really is busy.

- [docs/engine.stats.md](docs/engine.stats.md) — Every living thing has a
  handful of base numbers: how strong, how fast, how sharp-eyed, how
  tough. They don't change minute to minute — they're the starting
  character sheet. Things like *how hard does a bite hit*, *how far can
  you see*, *how often do you get to act* are all computed from these.
  Health and energy are separate — those *do* go up and down.

- [docs/engine.movement.md](docs/engine.movement.md) — How creatures get
  around on the grid. They can step in any of eight directions, including
  diagonals, and a diagonal step is treated as the same distance as a
  cardinal one. Covers chasing a target, fleeing a threat, wandering at
  random, and finding paths around obstacles. Also a subtle trap where
  "I can see you" and "I can walk to you" aren't the same thing — a
  hungry wolf on the wrong side of a pond has learned this.

- [docs/engine.species.md](docs/engine.species.md) — How the engine knows
  whether two animals are the same species. There's no species list or
  enum; instead, every creature remembers the function that spawned it,
  and two creatures count as the same species if they were born by the
  same function. That's enough for mating, hunting, and population caps.
  Adding a new predator-prey relationship takes almost no setup.

- [docs/engine.spatial-index.md](docs/engine.spatial-index.md) — The
  engine keeps a map of "who's at this cell?" so it can answer instantly
  instead of scanning everyone. The tricky part is keeping that map in
  sync when things move. There are a few well-defined ways to place,
  move, or destroy an entity, and they all update the map correctly. If
  you ever try to sneak around them, the map quietly goes wrong and
  nothing will warn you.

### Engine — how to extend it

- [docs/engine.filestructure.md](docs/engine.filestructure.md) — A map of
  the engine's folders and what lives in each. The guiding rule: one file
  per feature. If you add "hunting", the marker that says "this thing is
  a predator" and the behavior that makes it hunt live next to each other
  in the same file, not scattered across the codebase.

- [docs/engine.add-entity.md](docs/engine.add-entity.md) — A recipe for
  adding a new creature or object to the world. Figure out what pieces it
  needs, write them, glue them together in an archetype, drop the
  archetype into a world somewhere. Has a hawk example that goes end to
  end, and a list of the usual rookie mistakes.

- [docs/engine.examplerun.md](docs/engine.examplerun.md) — A step-by-step
  trace of what the computer actually does when you start the program and
  press tick a few times. Useful when you want to stop thinking about
  design and just see the sequence of events — startup, a rabbit's first
  turn, and later a rabbit having a baby.

## Running

```bash
dotnet run --project Console   # text frontend — see docs/console.readme.md
dotnet run --project Game      # GUI frontend  — see docs/gui.readme.md
```
