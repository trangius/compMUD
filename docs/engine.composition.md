# Composition

The rules below trace back to one decision: an entity is nothing but an
integer id, and its meaning lives entirely in the components attached to
it. Strip the components and the integer is meaningless; attach a new
one and the entity becomes something it was not a moment before.
Everything below follows from it.

## The rules

- **Components carry behavior.** Logic for a concern lives on the
  component that represents the concern. Not in a manager class, not in
  free functions that scan entities from outside.
- **Behaviors act directly.** `RunFromPredatorBehavior` moves the entity
  by calling `MovementHelper.MoveAwayFrom`. If a behavior decides
  something should happen, it happens in the same tick, by the behavior
  itself. No queue, no deferred resolution, no central class that turns
  requests into effects.
- **Entities pick one action per tick.** Every entity with `Behaviors`
  has a list of them. On the entity's turn, the dispatcher asks each
  behavior whether it wants to act and runs the highest-priority willing
  one. Exactly one action runs; the rest wait for next tick. The rule
  is enforced structurally — the dispatcher loop is the only path that
  calls behaviors.
- **A component and its behavior live in the same file.** `Hunt.cs`
  holds `Predator` (the marker that says "I hunt"), `Melee` (the
  component that owns the bite-damage formula), and `HuntBehavior`
  (what they do on a turn). One file per feature.
- **Single-instance classes are static.** There is one `World`, so
  `World` is a static class. No `new World()`, no instance passed
  around. Same for `Archetypes` and `Algorithms`.
- **Entities are integer ids with components attached dynamically.**
  This is what lets a rabbit gain a `Weapon` component at runtime by
  picking up a sword, or lose `RunFromPredator` from a courage buff.
  Composition is mutable.
- **One source of truth.** Don't cache derived flags like `IsHungry`.
  Let a behavior that needs to know ask `Diet.IsHungry(energy)` and
  read the current numbers. A cached flag can drift out of sync with
  the source.
- **No data-only classes.** A class that holds fields and nothing else
  is a sign the logic belongs somewhere else, or is missing. The one
  documented exception is category singletons (see
  `engine.five-buckets.md`).

## The C# shape

A rabbit is built by a factory function in `Archetypes.cs`:

```csharp
public static int CreateRabbit(int x, int y)
{
    int e = World.CreateEntity();
    World.AttachComponent(e, new Position(x, y));
    World.AttachComponent(e, new Health(/* max */));
    World.AttachComponent(e, new Species { spawn = CreateRabbit });
    World.AttachComponent(e, new Behaviors(
        new RunFromPredatorBehavior(),
        new FeedBehavior(rng),
        // ...
    ));
    World.AttachComponent(e, new Effects(new EnergyDrainEffect()));
    return e;
}
```

The function returns an `int` — that integer *is* the rabbit. Its
components live in `World`'s dictionaries, keyed by component type and
id, not as fields on a `Rabbit` object.

To use a component later, read it back through `World`:

```csharp
if (World.HasComponent<Health>(e))
{
    Health h = World.GetComponent<Health>(e);
    h.TakeDamage(damageAmount);
}
```

There is no `Rabbit` class and no base class above it. A rabbit is a
rabbit because of what is attached to its integer id inside `World`.

## When you write code

Don't write a function that loops over many entities from outside and
mutates them. That breaks every rule above. Do write a behavior or an
effect that acts on one entity at a time, called by the dispatcher from
inside the tick loop.

When you read the code, expect the logic for any given concern to live
on the component that owns that concern — or, for timed decisions, on
the behavior paired with it. There is no central orchestrator to go
looking in.
