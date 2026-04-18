# Example run — what the CPU actually does

Trace of what happens when `Console/Program.cs` starts and advances a few
ticks. From the call-stack perspective, not the design perspective —
`engine.composition.md` covers the "why", this one covers the "what".

## Startup

```csharp
// Program.cs entry
World.Initialize(60, 30);
HomeArea.StartingArea();
// then the command REPL
```

### `World.Initialize(60, 30)`
- `World.mapWidth = 60`, `World.mapHeight = 30`. Just two assignments.
- No entities yet, no dictionaries populated.

### `HomeArea.StartingArea(seed = 42)`

```csharp
Random rng = new Random(42);

// Border walls + interior grass
for each (x, y) in 60x30:
    if edge: Archetypes.CreateWall(x, y);
    else:    Archetypes.CreateGrass(x, y);
```

Each `Create*` call:
1. `World.CreateEntity()` → new integer id, added to `entities` HashSet.
2. A few `World.AttachComponent(id, ...)` calls. The first `Position`
   `AttachComponent` ALSO adds the entity to `spatialIndex[(x,y)]` (the one
   place that writes to the index besides `MoveEntity`).

After this loop: ~1800 entities, all positioned, most holding a `Position`,
`Appearance`, and one of {`Walkable` (grass), nothing (wall)}.

```csharp
// Pond: destroys grass cells in an ellipse, replaces with water
for each (x, y) in pond-ellipse:
    foreach (int existing in World.EntitiesAt(x, y))
        if Walkable: World.DestroyEntity(existing);    // grass gone
    Archetypes.CreateWater(x, y);                      // water entity placed
```

`DestroyEntity` walks all component stores removing the id, removes it from
`spatialIndex` if positioned, and from `entities`. Each destroyed grass is
replaced by a water entity at the same cell.

```csharp
// Trees (dense NW forest + scattered)
for i in 0..forestTrees:
    rng.Next(2, width/3), rng.Next(2, height*2/3) → x, y
    if IsOpenGround(x, y): Archetypes.CreateTree(x, y);

// same loop for bushes, rabbits, wolf-raid spawner
```

`CreateRabbit` and `CreateBush` are the heavy archetypes — they attach 10+
components each, including a `Behaviors` list (which itself is a List of
`IBehavior`) and an `Effects` list.

After `StartingArea`:
- `entities`: ~1900 integer ids (terrain + a handful of creatures + spawner).
- `components[typeof(Behaviors)]`: ~75 entries — rabbits, bushes, the wolf
  raid spawner… wait, spawner has no Behaviors, only Effects. So ~72
  (rabbits + bushes only).
- `components[typeof(Effects)]`: ~25 entries (rabbits + the raid spawner;
  bushes have no Effects).
- `components[typeof(Scheduler)]`: rabbits + bushes only (spawner has no
  Scheduler because it has no Behaviors — only Effects need a Scheduler
  if-and-only-if they pace behavior).

## The command REPL

```csharp
while (true) {
    string input = Console.ReadLine();
    // parse → look, tick, status, info, log, quit
    if command == "tick" and parts[1] == "1":
        World.Tick();  // <-- here we go
        RenderMap();
}
```

## Inside `World.Tick()` — Pass 1, one entity

Let's say `tickCount == 0` and we're dispatching rabbit id 42.

```csharp
// Snapshot the dispatchable entities
List<int> behaviorsList = components[typeof(Behaviors)].Keys.ToList();

foreach (int id in behaviorsList) {
    if (!EntityExists(id)) continue;

    // Scheduler gate
    if (HasComponent<Scheduler>(id) && !GetComponent<Scheduler>(id).IsDue(tickCount))
        continue;
    // rabbit 42's Scheduler: period=15, nextActTick=0. 0 >= 0 → due.

    // Ask each behavior in the rabbit's Behaviors list
    IBehavior winner = null;
    int best = int.MinValue;
    foreach (IBehavior b in GetComponent<Behaviors>(42).list) {
        if (b.WouldAct(42) && b.Priority > best) {
            winner = b;
            best = b.Priority;
        }
    }
    winner?.Act(42);

    if (EntityExists(42) && HasComponent<Scheduler>(42))
        GetComponent<Scheduler>(42).Reschedule(0);
    // nextActTick = 0 + 15 = 15.
}
```

Rabbit 42's Behaviors list:
`[EscapeGrappleBehavior, RunFromPredatorBehavior, HarvestBehavior, FeedBehavior, BreedBehavior, RestBehavior, WanderBehavior]`

Each `WouldAct` is a full method — they read world state via
`HasComponent`/`GetComponent`/`EntitiesAt`, maybe call `FindNearestEntity` or
`Algorithms.BFS`, sometimes set cached fields.

- `RunFromPredatorBehavior.WouldAct(42)`: calls `FindNearestEntity` with a filter that
  scans cells within Euclidean 15 looking for any entity with a `Predator`
  component whose `hunts` set contains `CreateRabbit`. No wolves right now.
  Returns false.
- `HarvestBehavior.WouldAct(42)`: `Diet.IsHungry(energy)` → `energy.Current <
  energy.Max * 0.6`. Rabbit spawned with Energy 1500 / 1500, still 1500 →
  not hungry. Returns false.
- `FeedBehavior.WouldAct(42)`: same hunger check, false.
- `BreedBehavior.WouldAct(42)`: cooldown check, energy check, then `Species.CountAll(CreateRabbit)` —
  iterates every entity with `Species`, counts matches. Then `FindAdjacentMate` —
  scans 8 neighbors. At tick 0, probably no mate adjacent. Then
  `FindNearestEntity` for a mate within Sensing. Probably finds one… but
  `breedChance` roll may skip. Say returns false on this tick.
- `RestBehavior.WouldAct(42)`: `energy.Current > energy.Max * 0.6` → true.
  Returns true.
- `WanderBehavior.WouldAct(42)`: always true (fallback).

Winner = `RestBehavior` (priority 1) over `WanderBehavior` (priority 0).
`RestBehavior.Act(42)` is a no-op by design.

`Reschedule(0)` → rabbit 42's `nextActTick = 15`. It won't act again until
`tickCount >= 15`.

## Inside `World.Tick()` — Pass 2

After all behaviors: Effects pass.

```csharp
foreach (int id in components[typeof(Effects)].Keys.ToList()) {
    if (!EntityExists(id)) continue;
    foreach (IEffect ef in GetComponent<Effects>(id).list) {
        if (!EntityExists(id)) break;
        ef.Apply(id);
    }
}
```

Rabbit 42 has one effect: `EnergyDrainEffect`.
- `energy.Drain()` — `Current -= 1`. Energy now 1499.
- If `Current <= 0`, apply damage. Not today.

The raid spawner entity also has Effects: `WolfRaidEffect`.
- `rng.NextDouble() >= 0.0015` → usually true (no raid this tick). Returns.
- Every ~670 ticks it returns false (roll succeeded), picks a random tree,
  calls `Archetypes.CreateWolf(treePos.X, treePos.Y)`. That `CreateWolf`
  is itself a big sequence of `AttachComponent` calls. The new wolf
  appears immediately, with `nextActTick = 0`, and will act on the NEXT
  `World.Tick()` call.

## A breeding tick (later on)

Suppose `tickCount == 400`, rabbit 42 (nextActTick=405) is skipped. Rabbit
51 is due, has Energy 1400/1500, off breeding cooldown, and has rabbit 68
adjacent. Its `BreedBehavior.WouldAct(51)`:

- `Species.CountAll(CreateRabbit)` returns, say, 11. Below cap of 12.
- `FindAdjacentMate(...)` finds rabbit 68.
- `rng.NextDouble() < 0.1` succeeds → roll passes. Caches mate, returns true.

Wins over Rest (priority 20 > 1). `BreedBehavior.Act(51)`:

```csharp
breeding.lastBreedTick = 400;                                 // own cooldown
World.GetComponent<Breeding>(68).lastBreedTick = 400;         // mate's cooldown
int baby = species.spawn(pos.X, pos.Y);                       // <-- THE Func<> call
// species.spawn IS Archetypes.CreateRabbit. Calling it:
//   - World.CreateEntity() — new int, say 1903
//   - AttachComponent(1903, new Position(...))
//   - AttachComponent(1903, new Appearance {...})
//   - ... (all the rabbit components)
//   - Returns 1903.
World.GetComponent<Breeding>(1903).lastBreedTick = 400;       // baby on cooldown
World.Log("Rabbit born at (...)");
```

Baby rabbit 1903 now exists. It has `Scheduler.nextActTick = 0` (default).
On `tickCount == 401`, it's due (0 <= 401), acts for the first time.

Meanwhile in Pass 2 this same tick, `EnergyDrainEffect` runs on rabbit 1903
too — it's in `AllWithComponent<Effects>()`, freshly added — and drains 1 of
its 1500 starting energy. (Babies eat their first meal the same way adults do.)

## Takeaway

Every "event" in the sim is a chain of method calls rooted at `World.Tick`:
dispatcher → behavior `WouldAct` → behavior `Act` → helper (`TryMove`, BFS,
`DestroyEntity`, species.spawn(...)) → component mutation. Nothing "runs on
its own" — time only passes when the frontend calls `World.Tick`.
