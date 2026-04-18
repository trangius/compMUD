# The spatial index: Position keeps itself in sync

`World` keeps a reverse lookup from cell `(x, y)` to entity ids so that
"who's at this cell?" is `O(1)` instead of scanning every Position. The rule
is: **the index is maintained by Position itself, via the generic
IOnAttach / IOnDetach hooks the component store calls.** Nothing else writes
to it.

## How the sync actually happens

`Position` implements `IOnAttach` and `IOnDetach`. When `AttachComponent`
stores a Position, it calls `OnAttach(id)`, which adds the entity to the
spatial index at `(X, Y)`. When `DetachComponent` or `DestroyEntity` drop a
Position, they call `OnDetach(id)` first, which removes it. Moving is
"detach the old Position, attach a new one" — one line each way in
`World.MoveEntity`.

The point: `World` knows nothing about Position. The generic dispatcher just
calls hooks on whatever components care. Other components (schedulers, in
particular) use the same hooks for their own init — `IOnAttach`/`IOnDetach`
are declared at the top of `World.cs`, right next to the store they plug into.

## The four legitimate paths

| Method | What happens to the index |
|---|---|
| `AttachComponent(id, new Position(x, y))` | Fires `Position.OnAttach` → adds the entity at `(x, y)`. If the entity already had a Position, the old one's `OnDetach` fires first (removing it from the old cell). |
| `MoveEntity(id, newX, newY)` | Detaches the old Position, attaches a new one. Spatial index updates via the hooks. |
| `DetachComponent<Position>(id)` | Fires `Position.OnDetach` → removes from the index. The entity still exists (integer id + other components); it just has no location. |
| `DestroyEntity(id)` | Iterates every component store, fires `OnDetach` on any component that implements it (Position among them), then drops the id. |

Any code that wants to move or place an entity goes through one of these.

## The trap

`World.components[typeof(Position)][id]` is reachable. **Never write to it
directly.** The moment you do:

```csharp
// DO NOT — bypasses the hooks, index goes out of sync
World.components[typeof(Position)][id] = new Position(newX, newY);
```

...the entity has a new `Position` component, but the spatial index still has
it listed at the old cell. `EntitiesAt(newX, newY)` doesn't find it;
`EntitiesAt(oldX, oldY)` still returns it. Every spatial query is now lying.
The bug is silent and compounding.

`World.AddToSpatialIndex` and `RemoveFromSpatialIndex` are `internal` so
only `Position.OnAttach` / `OnDetach` can call them — the compiler keeps
you honest.

## Public read-only surface

Reading is cheap and free-for-all:

- `EntitiesAt(x, y)` — list of entity ids at a cell (copy, safe to mutate caller-side).
- `HasComponent<T>(id)`, `GetComponent<T>(id)` — standard component access.
- `AllWithComponent<T>()` — every entity with component T (snapshot list).
- `FindNearestEntity(x, y, range, filter)` — Euclidean-radius search.
- `FindCell(predicate, rng)` — pick a random cell matching a predicate.
- `IsOpenGround(x, y)`, `CanCreatureBeHere(x, y)` — passability predicates.

## Consequences

- Passing bare `Position` objects around is safe — they're immutable.
- A behavior that wants to move its entity calls `MovementHelper.TryMove`,
  which ends up calling `MoveEntity`. That's the chain; trust it.
- If you ever find yourself tempted to "just update the component", stop and
  use one of the four methods instead.
