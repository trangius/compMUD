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

## BFS pathfinding

[`Engine/Spatial/Algorithms.cs`](../Engine/Spatial/Algorithms.cs) holds the generic flood-fill:

```csharp
BFSResult bfs = Algorithms.BFS(startX, startY, maxRange, isPassable, rng);
// bfs.Reachable(x, y)      — did the flood reach (x, y)?
// bfs.Distance(x, y)       — how many 8-connected steps to get there?
// bfs.FirstStep(gx, gy)    — (dx, dy) of the first move along the shortest path
```

`isPassable` is supplied by the caller — that's the extension point for
swimmer / climber creatures later. Today everyone passes [`World.CanCreatureBeHere`](../Engine/World.cs#L355)
(has Walkable, no Solid).

The optional `rng` shuffles neighbor-iteration order once per call. BFS still
finds optimal paths, but which of several equal-length paths survives in
`cameFrom` (and so which step `FirstStep` returns) varies. Without that,
many callers flooding toward the same goal picked identical first steps
and lined up. Omit `rng` and the flood is deterministic.

**Diagonals cost 1**, same as cardinals. The returned `Distance` is Chebyshev
distance. Slight "cheat" on long paths (a diagonal crossing should really be
~1.41×), but the game is grid-turn-based and the simplification lets
diagonals behave naturally in movement and BFS alike.

## Who uses what

| Behavior | Target discovery | Stepping |
|---|---|---|
| [`FeedBehavior`](../Engine/Behaviors/Feeding.cs#L43) | BFS — scans reached cells for edibles | `TryMove` with cached first-step |
| [`HuntBehavior`](../Engine/Behaviors/Hunt.cs#L60) | BFS — scans reachable neighbors of prey | `TryMove` with cached first-step |
| [`ReturnToForestBehavior`](../Engine/Behaviors/WolfRaid.cs#L17) | BFS — nearest reachable [`Tree`](../Engine/Tree.cs#L5) | `TryMove` with cached first-step |
| [`RunFromPredatorBehavior`](../Engine/Behaviors/RunFromPredator.cs#L9) | [`FindNearestEntity`](../Engine/World.cs#L310) (Euclidean circle) | `MoveAwayFrom` (one-step greedy) |
| [`BreedBehavior`](../Engine/Behaviors/Breeding.cs#L15) | `FindNearestEntity` | `MoveToward` (one-step greedy) |
| [`WanderBehavior`](../Engine/Behaviors/Wander.cs#L4) | — | `Wander` (uniform random) |

## Vision vs reachability

[`StatMath.VisionRange(id)`](../Engine/Stats/StatMath.cs#L29) returns one number (= [`Stats.Perception`](../Engine/Stats/Stats.cs#L17)), but two
functions interpret it differently:

- **[`FindNearestEntity(x, y, range, filter)`](../Engine/World.cs#L310)** — scans a `[-range, +range]²`
  box, filters by `dx² + dy² ≤ range²`. That's a **Euclidean disk**.
- **[`Algorithms.BFS(x, y, maxRange, ...)`](../Engine/Spatial/Algorithms.cs#L28)** — flood of 8-connected steps up
  to `maxRange`. That's a **Chebyshev square**.

Consequences:

- The Euclidean disk fits *inside* the Chebyshev square — anything visible by
  Euclidean is also reachable by BFS, assuming no obstacles.
- With obstacles, a prey can be "visible" (Euclidean) but unreachable (BFS
  needs > maxRange steps around a pond). `HuntBehavior` dodges this by
  BFS-first target discovery — it only considers prey it can path to. `RunFromPredatorBehavior` / `BreedBehavior` still use Euclidean; they don't
  path-commit, so "see but can't reach" isn't a bug for them.

## 8-connected adjacency

"Adjacent" means Chebyshev ≤ 1, i.e. any of the 8 surrounding cells. Not
Manhattan ≤ 1. Used by:

- `HuntBehavior` — bite when prey is Chebyshev-adjacent.
- [`BreedBehavior.FindAdjacentMate`](../Engine/Behaviors/Breeding.cs#L133) — mate is on any of the 8 neighbors.
- `ReturnToForestBehavior` — a tree on the same cell or next-door.

If you're writing a new proximity check, prefer `Math.Max(Math.Abs(dx), Math.Abs(dy))`.
