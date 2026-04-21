# Movement

The engine is **8-connected** — every move (step, flee, flood, spread) considers
4 cardinals + 4 diagonals, and diagonals cost the same as cardinals.

## The core helpers

All in [`Engine/Spatial/Spatial.cs`](../Engine/Spatial/Spatial.cs) ([`MovementHelper`](../Engine/Spatial/Spatial.cs#L34)):

- **[`TryMove(id, dx, dy)`](../Engine/Spatial/Spatial.cs#L48)** — one-step primitive. Target cell must have a
  [`Walkable`](../Engine/Spatial/Spatial.cs#L25) occupant and no [`Solid`](../Engine/Spatial/Spatial.cs#L29). Returns bool (succeeded / blocked).
  Works with any `(dx, dy)` — cardinal, diagonal, or zero.
- **[`MoveToward(id, pos, tx, ty)`](../Engine/Spatial/Spatial.cs#L81)** — step toward a target. Tries the diagonal
  step first when both axes want progress (that IS the shortest path on a
  uniform-cost 8-grid). If diagonal is blocked, falls through to the bigger
  single-axis move, then the other single-axis. No randomness; deterministic.
- **[`MoveAwayFrom(id, pos, threatX, threatY, rng)`](../Engine/Spatial/Spatial.cs#L113)** — pick the passable
  neighbor that maximizes squared-Euclidean distance from the threat. Tries
  all 8, picks best; ties break randomly via the supplied rng. Without the
  random tiebreak, many creatures fleeing one threat all picked the same
  first-in-array direction and lined up in parallel. Cardinal, diagonal —
  whatever gets farthest. Fixes the old "run to wall, stop" bug: if east
  is blocked, picks a diagonal or the other axis.
- **[`Wander(id, rng)`](../Engine/Spatial/Spatial.cs#L151)** — random step. Picks one of 8 neighbors uniformly; if
  blocked, stays put. Caller supplies rng for determinism.

## Flow fields (multi-source BFS)

Perception is all done via flow fields. "Find nearest X" never means a
per-creature scan — it means reading a flow field that was flooded once
this tick from every X on the map. Every consumer that asks for the same
kind of X gets the same cached result.

[`Engine/Spatial/Algorithms.cs`](../Engine/Spatial/Algorithms.cs) holds the flood:

```csharp
FlowField f = Algorithms.MultiSourceBFS(sourceCells, isPassable);
// f.Reachable(x, y)    — did the flood reach (x, y)?
// f.Distance(x, y)     — steps from (x, y) to the nearest source
// f.StepToward(x, y)   — unit direction from (x, y) one step closer to source
```

Seed cells skip the passability test (a prey cell is Solid but still a
valid seed). All other cells must pass `isPassable`. Diagonals cost 1 —
distances are Chebyshev.

Consumers don't call `MultiSourceBFS` directly. They ask `World` for the
flow field they need, cached per tick:

| Lookup | Sources |
|---|---|
| `World.GetSpeciesFlowField(spawn)` | entities where `Species.spawn == spawn` |
| `World.GetYieldFlowField(category)` | entities with `Yields` containing `category` and no `Health` |
| `World.GetPredatorsHuntingFlowField(prey)` | entities where `Predator.Hunts(prey)` is true |
| `World.GetComponentFlowField<T>()` | entities with component `T` (marker) |

Reading a flow field happens via [`FlowFieldHelper`](../Engine/Spatial/Algorithms.cs):

```csharp
// "Step toward the nearest source" — Hunt, Feed, ReturnToForest
FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, maxRange, rng, out step);

// "Step AWAY from the nearest source" — RunFromPredator
FlowFieldHelper.PickFarthestNeighborStep(pos.X, pos.Y, fields, isPassable, visionRange, rng, out step);
```

Both scan the caller's 8 neighbors — the caller's own cell is Solid so it
never appears in a flow field. Random tiebreak on equally-good neighbors
keeps packs from funneling through one cell.

Why flow fields instead of per-creature BFS: at N creatures, per-creature
BFS is O(N × vision²); one multi-source BFS is O(reachable map cells),
independent of N. The cost shape stops growing with population.

## Who uses what

| Behavior | Perception | Stepping |
|---|---|---|
| [`HuntBehavior`](../Engine/Behaviors/Hunt.cs) | `GetSpeciesFlowField(prey)` → `PickNearestNeighborStep` | `TryMove` with cached step |
| [`FeedBehavior`](../Engine/Behaviors/Feeding.cs) | `GetYieldFlowField(category)` → `PickNearestNeighborStep` | `TryMove` with cached step |
| [`RunFromPredatorBehavior`](../Engine/Behaviors/RunFromPredator.cs) | `GetPredatorsHuntingFlowField(myspecies)` → `PickFarthestNeighborStep` | `TryMove` with cached step |
| [`ReturnToForestBehavior`](../Engine/Behaviors/WolfRaid.cs) | `GetComponentFlowField<Tree>()` → `PickNearestNeighborStep` | `TryMove` with cached step |
| [`BreedBehavior`](../Engine/Behaviors/Breeding.cs) | [`FindNearestEntity`](../Engine/World.cs) (Euclidean — same-species mate) | `MoveToward` (one-step greedy) |
| [`WanderBehavior`](../Engine/Behaviors/Wander.cs) | — | `Wander` (uniform random) |

## Vision vs reachability

[`StatMath.VisionRange(id)`](../Engine/Stats/StatMath.cs#L29) returns one number (= [`Stats.Perception`](../Engine/Stats/Stats.cs#L17)), but two
functions interpret it differently:

- **[`FindNearestEntity(x, y, range, filter)`](../Engine/World.cs)** — scans a `[-range, +range]²`
  box, filters by `dx² + dy² ≤ range²`. That's a **Euclidean disk**.
- **Flow-field distance** — graph distance in 8-connected steps, respecting
  obstacles (unreachable cells have no entry at all). That's **Chebyshev**.

Consequences:

- The Euclidean disk fits inside the Chebyshev square — anything visible by
  Euclidean is also reachable by flow field, assuming no obstacles.
- With obstacles, a target can be "visible" (Euclidean) but unreachable
  (no flood path around a pond). Flow-field consumers naturally skip
  unreachable targets. `BreedBehavior` is the only remaining Euclidean
  consumer — mating doesn't path-commit, so "see but can't reach" isn't a
  bug there.

## 8-connected adjacency

"Adjacent" means Chebyshev ≤ 1, i.e. any of the 8 surrounding cells. Not
Manhattan ≤ 1. Used by:

- `HuntBehavior` — bite when prey is Chebyshev-adjacent (distance 0 in the
  prey flow field at a neighbor cell).
- [`BreedBehavior.FindAdjacentMate`](../Engine/Behaviors/Breeding.cs) — mate is on any of the 8 neighbors.
- `ReturnToForestBehavior` — a tree on the same cell (despawn) or one step
  away (step there next tick).

If you're writing a new proximity check, prefer `Math.Max(Math.Abs(dx), Math.Abs(dy))`.
