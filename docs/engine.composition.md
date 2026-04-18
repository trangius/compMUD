# Composition, not ECS

`World` holds entities and components, but this is NOT an Entity-Component-System
engine. The difference matters — if you treat it as ECS, you'll write the wrong
code.

## The rules

- **Components carry behavior.** `Health` has `TakeDamage()`. `Energy` has
  `Drain()` and `Restore()`. `Species` has `CountAll()`, `CountInRadius()`.
  Components are small objects with methods, not dumb data bags manipulated
  by external systems.
- **Behaviors act directly.** `RunFromPredatorBehavior` moves the entity by calling
  `MovementHelper.MoveAwayFrom(...)`. No intents, no middlemen, no deferred
  resolution queue. If a behavior wants something to happen, it makes it
  happen that tick.
- **Entities pick ONE action per tick.** Each entity has a `Behaviors` list of
  `IBehavior` implementations. `World.Tick` asks each `WouldAct`, then runs
  `Act` on the highest-priority willing behavior. Exactly one action per
  entity per tick, enforced structurally (see `engine.tick.md`).
- **Component + its behavior live in the same file.** `Hunt.cs` has the
  marker (`Predator`), the companion component (`Attacking`), AND the behavior
  (`HuntBehavior`) together. One file, one feature.
- **Single-instance classes are static.** One `World` → static class. No `new
  World()`, no passing `world` around. Same for `Archetypes`, `Algorithms`.
- **Entities are integer IDs** with components in `World`'s dictionaries. This
  enables dynamic composition — a rabbit can gain `Attacking` at runtime by
  picking up a sword, lose `Flees` by buffing courage, etc.
- **One source of truth.** Don't expose derived booleans (e.g. `IsHungry`,
  `IsStarving`) that can disagree with the underlying data. If a behavior
  needs to know whether an entity is hungry, let it ask `Diet.IsHungry(energy)`
  — a method that reads the current values. No cached flags.
- **No data-only classes.** If a class only holds fields with no methods, push
  logic onto it. (`Category`-style singleton labels are the documented
  exception — see `engine.five-buckets.md`.)

## The C# shape

Preferred:
```csharp
class Rabbit
{
    public IMovement movement;
    public IAttack attack;
    public IPolarity polarity;
    // ...
}
```

Avoided:
```csharp
class Rabbit : Creature : Entity : GameObject { ... }
```

Interfaces describe what something *can do*, not what it *is*. A class
implements only the interfaces relevant to its behavior. There's no
`AbstractCreature` base class — a rabbit is a rabbit because of the components
attached to its integer id in `World`, not because of its class hierarchy.

## Why it matters for you

When adding something new:

- **Don't** write a system class that iterates entities and mutates their
  fields. That's ECS.
- **Do** write a behavior or effect that acts on one entity at a time from
  inside the per-entity dispatch loop. That's composition.

When reading code:

- Logic that affects entity X lives in a component on entity X (or a behavior
  that owns X for the tick). It doesn't live in a manager singleton that
  "processes" X.
