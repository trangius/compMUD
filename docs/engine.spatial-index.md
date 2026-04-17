# The spatial index: four paths in, no side doors

`World` keeps a reverse lookup from cell `(x, y)` to entity ids so that
"who's at this cell?" is `O(1)` instead of scanning every Position. The rule
is: **exactly four public methods maintain that index. Nothing else is
allowed to write to it.**

## The four legitimate paths

| Method | What it does to the index |
|---|---|
| `AttachComponent(id, new Position(x, y))` | Adds the entity at `(x, y)`. If the entity was already placed, removes from the old cell first. |
| `MoveEntity(id, newX, newY)` | Removes from old cell, writes the new `Position`, adds to the new cell. |
| `DetachComponent<Position>(id)` | Takes the entity off the map. The entity still exists (still has an integer id and other components); it just has no location. |
| `DestroyEntity(id)` | If positioned, removes from the index. Then wipes all components and the id. |

That's the complete public surface. Any code that wants to move or place an
entity goes through one of these.

## The trap

`World.components[typeof(Position)][id]` is reachable. It's the dictionary
that stores each entity's Position object. **Never write to it directly.**
The moment you do:

```csharp
// DO NOT — bypasses the index
World.components[typeof(Position)][id] = new Position(newX, newY);
```

...the entity now has a new `Position` component, but the spatial index
still has it listed at the old cell. Subsequent `EntitiesAt(newX, newY)` calls
don't find it; `EntitiesAt(oldX, oldY)` still returns it. Every spatial
query is now lying. The bug is silent and compounding.

`MoveEntity` is the only code allowed to write `components[Position][id]`,
and it does so *alongside* updating the index. Atomic; correct.

## Public read-only surface

Reading is cheap and free-for-all:

- `EntitiesAt(x, y)` — list of entity ids at a cell (copy, safe to mutate caller-side).
- `HasComponent<T>(id)`, `GetComponent<T>(id)` — standard component access.
- `AllWithComponent<T>()` — every entity with component T (snapshot list).
- `FindNearestEntity(x, y, range, filter)` — Euclidean-radius search.
- `FindCell(predicate, rng)` — pick a random cell matching a predicate.
- `IsOpenGround(x, y)`, `IsCreatureSpawnable(x, y)` — passability predicates.

## Consequences

- Passing bare `Position` objects around is safe — they're immutable.
- A behavior that wants to move its entity calls `MovementHelper.TryMove`,
  which ends up calling `MoveEntity`. That's the chain; trust it.
- If you ever find yourself tempted to "just update the component", stop and
  use one of the four methods instead.
