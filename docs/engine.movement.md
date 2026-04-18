# Movement

The engine is **8-connected** — every move (step, flee, flood, spread) considers
4 cardinals + 4 diagonals, and diagonals cost the same as cardinals. Clusters
look blobby, paths bend naturally, random walks round out.

## The core helpers

All in `Engine/Spatial/Spatial.cs` (`MovementHelper`):

- **`TryMove(id, dx, dy)`** — one-step primitive. Target cell must have a
  `Walkable` occupant and no `Solid`. Returns bool (succeeded / blocked).
  Works with any `(dx, dy)` — cardinal, diagonal, or zero.
- **`MoveToward(id, pos, tx, ty)`** — step toward a target. Tries the diagonal
  step first when both axes want progress (that IS the shortest path on a
  uniform-cost 8-grid). If diagonal is blocked, falls through to the bigger
  single-axis move, then the other single-axis. No randomness; deterministic.
- **`MoveAwayFrom(id, pos, threatX, threatY)`** — pick the passable neighbor
  that maximizes squared-Euclidean distance from the threat. Tries all 8,
  picks best. Cardinal, diagonal — whatever gets farthest. Fixes the old
  "run to wall, stop" bug: if east is blocked, picks a diagonal or the
  other axis.
- **`Wander(id, rng)`** — random step. Picks one of 8 neighbors uniformly; if
  blocked, stays put. Caller supplies rng for determinism.

## BFS pathfinding

`Engine/Spatial/Algorithms.cs` holds the generic flood-fill:

```csharp
BFSResult bfs = Algorithms.BFS(startX, startY, maxRange, isPassable);
// bfs.Reachable(x, y)      — did the flood reach (x, y)?
// bfs.Distance(x, y)       — how many 8-connected steps to get there?
// bfs.FirstStep(gx, gy)    — (dx, dy) of the first move along the shortest path
```

`isPassable` is supplied by the caller — that's the extension point for
swimmer / climber creatures later. Today everyone passes `World.CanCreatureBeHere`
(has Walkable, no Solid).

**Diagonals cost 1**, same as cardinals. The returned `Distance` is Chebyshev
distance. Slight "cheat" on long paths (a diagonal crossing should really be
~1.41×) but the game is grid-turn-based and the visual benefit beats the
realism loss.

## Who uses what

| Behavior | Target discovery | Stepping |
|---|---|---|
| `FeedBehavior` | BFS — scans reached cells for edibles | `TryMove` with cached first-step |
| `HuntBehavior` | BFS — scans reachable neighbors of prey | `TryMove` with cached first-step |
| `ReturnToForestBehavior` | BFS — nearest reachable `Tree` | `TryMove` with cached first-step |
| `FleeBehavior` | `FindNearestEntity` (Euclidean circle) | `MoveAwayFrom` (one-step greedy) |
| `BreedBehavior` | `FindNearestEntity` | `MoveToward` (one-step greedy) |
| `WanderBehavior` | — | `Wander` (uniform random) |

## The range-shape gotcha

`Sensing.VisionRange` is one number, but two functions interpret it differently:

- **`FindNearestEntity(x, y, range, filter)`** — scans a `[-range, +range]²`
  box, filters by `dx² + dy² ≤ range²`. That's a **Euclidean disk**.
- **`Algorithms.BFS(x, y, maxRange, ...)`** — flood of 8-connected steps up
  to `maxRange`. That's a **Chebyshev square** (31×31 at range 15).

Consequences:

- The Euclidean disk fits *inside* the Chebyshev square — anything visible by
  Euclidean is also reachable by BFS, assuming no obstacles.
- With obstacles, a prey can be "visible" (Euclidean) but unreachable (BFS
  needs > maxRange steps around a pond). `HuntBehavior` dodges this by
  BFS-first target discovery — it only considers prey it can actually path
  to. `FleeBehavior` / `BreedBehavior` still use Euclidean; they don't
  path-commit, so "see but can't reach" isn't a bug for them.

## 8-connected adjacency

"Adjacent" means Chebyshev ≤ 1, i.e. any of the 8 surrounding cells. Not
Manhattan ≤ 1. Used by:

- `HuntBehavior` — bite when prey is Chebyshev-adjacent.
- `BreedBehavior.FindAdjacentMate` — mate is on any of the 8 neighbors.
- `ReturnToForestBehavior` — a tree on the same cell or next-door.

If you're writing a new proximity check, prefer `Math.Max(Math.Abs(dx), Math.Abs(dy))`.
