# How to add a new entity

Walkthrough for adding a new creature / object. Keep the "five buckets"
taxonomy open while you do this — every piece of the new thing slots into
one of them.

## Step 1 — Decide what buckets you need

Ask for each aspect of the new thing:

- **Does it have a position?** → an **Entity** (almost always yes).
- **Is there a new "kind" of property?** → a **State** component.
- **Does it decide things per-tick?** → one or more **Behaviors**.
- **Do things happen to it automatically?** → one or more **Effects**.
- **Does it reference a shared label (a food kind, a damage type)?** →
  a **Category** — usually reuse an existing one, add a new one only when
  genuinely needed.

## Step 2 — Create the files that don't exist yet

**New State components**: file-per-feature. If the state is used by a
behavior, put both in the same file in `Engine/Behaviors/`. If it's
cross-cutting (like `Species`), give it its own file at `Engine/`.

**New Behaviors**: one class per behavior, in `Engine/Behaviors/Foo.cs`.
Implement `IBehavior` — fields for cached target info, `Priority` property,
`WouldAct(int id)`, and `int Act(int id)`. `Act` returns the action's cost
(1 = baseline; return higher to make the action take longer before the
entity's next turn — see `engine.scheduler.md`).

**New Effects**: `Engine/Effects/Foo.cs` (or beside the behavior it pairs
with — judgment call). Implement `IEffect` — `Apply(int id)`.

**New Categories**: add an instance to the relevant registry singleton
(`Resources`, future `Materials`, etc.).

## Step 3 — Add the archetype

In `Engine/Archetypes.cs`, add a `Create*` static method. Pattern:

```csharp
// ----------------------------------------------------------------------------
// A hawk: flies fast, hunts rabbits and mice, returns to cliffs to rest.
// ----------------------------------------------------------------------------
public static int CreateHawk(int x, int y)
{
    int e = World.CreateEntity();
    World.AttachComponent(e, new Position(x, y));
    World.AttachComponent(e, new Appearance { spriteId = "hawk", layer = 4 });
    World.AttachComponent(e, new Named { name = "Hawk" });
    World.AttachComponent(e, new Solid());
    World.AttachComponent(e, new Species { spawn = CreateHawk });     // species ID
    World.AttachComponent(e, new Scheduler { period = 5 });           // fast
    World.AttachComponent(e, new Predator(CreateRabbit, CreateMouse)); // hunt list
    World.AttachComponent(e, new Sensing(40));
    World.AttachComponent(e, new Health(8));
    World.AttachComponent(e, new Energy(500));
    World.AttachComponent(e, new Attacking(6));
    World.AttachComponent(e, new Diet(Resources.Meat));
    World.AttachComponent(e, new Behaviors(
        new HuntBehavior(),
        new HarvestBehavior(),
        new FeedBehavior(rng),
        new WanderBehavior(rng)
    ));
    World.AttachComponent(e, new Effects(new EnergyDrainEffect()));
    return e;
}
```

The returned `int e` IS the entity. Store it if you need to reference it
later; otherwise discard.

## Step 4 — Wire into an area

In `Engine/Areas/HomeArea.cs` (or a new area), call the new archetype where
you want it to appear. For creature populations, use `World.FindCell` with
any predicate you want to enforce (open ground, min distance from existing
entities, etc.).

## Step 5 — Update docs

Consult `README.md` for which doc(s) to update:

- New archetype → `engine.filestructure.md` (if a new file), `engine.add-entity.md` (this file — add to examples if notable).
- New predator-prey pair → `engine.species.md`.
- New behavior → `engine.five-buckets.md` (examples), plus its own mention.

## Common mistakes

- **Forgetting `Species`**. If anything asks "is this my kind?" (mating,
  hunting, clustering), the entity needs a `Species` component. Bushes
  without one won't cluster-cap; wolves without one can't be targeted by
  predator sets.
- **Forgetting `Scheduler`**. The entity will default to acting every global
  tick — fine for "lightspeed" creatures, usually wrong for anything else.
- **Putting logic in a manager/system class.** See `engine.composition.md`.
  Logic goes in the component or the behavior on the entity, not in an
  external system.
- **Reusing an existing component for a new concept.** If the new meaning is
  genuinely different, make a new component. Shoehorning causes subtle bugs
  when another code path reads the component the original way.
