# The spatial index

[`World`](../Engine/World.cs#L26) keeps a reverse lookup from cell `(x, y)` to entity ids so that
"who's at this cell?" is `O(1)` instead of scanning every Position. The rule
is: **the index is maintained by Position itself, via the generic
IOnAttach / IOnDetach hooks the component store calls.** Nothing else writes
to it.

## How the sync happens

[`Position`](../Engine/Spatial/Spatial.cs#L9) implements [`IOnAttach`](../Engine/World.cs#L12) and [`IOnDetach`](../Engine/World.cs#L17). When [`AttachComponent`](../Engine/World.cs#L183)
stores a Position, it calls [`OnAttach(id)`](../Engine/Spatial/Spatial.cs#L20), which adds the entity to the
spatial index at `(X, Y)`. When [`DetachComponent`](../Engine/World.cs#L206) or [`DestroyEntity`](../Engine/World.cs#L165) drop a
Position, they call [`OnDetach(id)`](../Engine/Spatial/Spatial.cs#L21) first, which removes it. Moving is
"detach the old Position, attach a new one" — one line each way in
[`World.MoveEntity`](../Engine/World.cs#L292).

`World` itself knows nothing about Position. The generic dispatcher just
calls hooks on whatever components care. Other components (schedulers, in
particular) use the same hooks for their own init — `IOnAttach`/`IOnDetach`
are declared at the top of [`World.cs`](../Engine/World.cs), right next to the store they plug into.

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
`EntitiesAt(oldX, oldY)` still returns it. Nothing warns you, and every
subsequent spatial query that touches either cell returns wrong results.

[`World.AddToSpatialIndex`](../Engine/World.cs#L386) and [`RemoveFromSpatialIndex`](../Engine/World.cs#L394) are `internal` so
only `Position.OnAttach` / `OnDetach` can call them — the compiler keeps
you honest.

## Public read-only surface

Reading is cheap and free-for-all:

- [`EntitiesAt(x, y)`](../Engine/World.cs#L281) — list of entity ids at a cell (copy, safe to mutate caller-side).
- [`HasComponent<T>(id)`](../Engine/World.cs#L220), [`GetComponent<T>(id)`](../Engine/World.cs#L228) — standard component access.
- [`AllWithComponent<T>()`](../Engine/World.cs#L238) — every entity with component T (snapshot list).
- [`AllWithComponents<T1, T2>()`](../Engine/World.cs#L248) — intersection of two component types.
  Iterates the smaller of the two stores for efficiency.
- [`FindNearestEntity(x, y, range, filter)`](../Engine/World.cs#L310) — Euclidean-radius search.
- [`FindCell(predicate, rng)`](../Engine/World.cs#L370) — pick a random cell matching a predicate.
  Returns `(-1, -1)` if none match within its attempt budget.
- [`IsOpenGround(x, y)`](../Engine/World.cs#L339), [`CanCreatureBeHere(x, y)`](../Engine/World.cs#L355) — passability predicates.

## Consequences

- Passing bare `Position` objects around is safe — they're immutable.
- A behavior that wants to move its entity calls [`MovementHelper.TryMove`](../Engine/Spatial/Spatial.cs#L48),
  which ends up calling `MoveEntity`.
- If you ever find yourself tempted to "just update the component", stop and
  use one of the four methods instead.
