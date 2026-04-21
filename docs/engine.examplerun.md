# Example run

A trace of what happens when [`Console/Program.cs`](../Console/Program.cs) starts and advances a
few ticks — the view from inside the engine as specific scenes play out.

## Startup

`Program.cs` calls [`World.Initialize(width, height)`](../Engine/World.cs#L52), then
[`HomeArea.StartingArea()`](../Engine/Areas/HomeArea.cs#L14), then enters the REPL.

`Initialize` just assigns [`mapWidth`](../Engine/World.cs#L42) and [`mapHeight`](../Engine/World.cs#L43) — two field writes,
no entities.

`StartingArea` builds the world in stages: border walls, interior grass,
a pond carved out of grass by destroying the grass entities in an ellipse
and creating water entities in their place, trees scattered (dense NW,
thin elsewhere), bushes, rabbits, and the wolf raid spawner.

Each archetype call creates a new entity ([`World.CreateEntity()`](../Engine/World.cs#L154) returns
a fresh integer id) and attaches components to it. The first `Position`
attach registers the entity in the spatial index at its cell.
[`DestroyEntity`](../Engine/World.cs#L165) walks every component store, fires `OnDetach` on anything
that implements it (removing from the spatial index is one such hook),
then drops the id.

After `StartingArea`, the world holds thousands of integer ids. Most are
terrain; the `Behaviors` store has one entry per rabbit and bush; the
`Effects` store has one per rabbit plus the wolf raid spawner (which
carries only `Effects`, no `Behaviors`).

## A quiet tick

The REPL calls [`World.Tick()`](../Engine/World.cs#L67). Pass 1 iterates every entity with
`Behaviors`. Take a well-fed rabbit with no wolves in sight.

Its [`AgilityPaced`](../Engine/Scheduler.cs#L33) scheduler reports it as due. It carries no [`Grappled`](../Engine/Behaviors/Grapple.cs#L8)
component. The dispatcher walks its behavior list and asks each
`WouldAct`:

- [`EscapeGrappleBehavior`](../Engine/Behaviors/Grapple.cs#L53) — not grappled. Declines.
- [`RunFromPredatorBehavior`](../Engine/Behaviors/RunFromPredator.cs#L9) — [`FindNearestEntity`](../Engine/World.cs#L310) in a Euclidean disk
  sized by [`Stats.Perception`](../Engine/Stats/Stats.cs#L17), filtered for any [`Predator`](../Engine/Behaviors/Hunt.cs#L7) whose
  `preySpecies` contains [`CreateRabbit`](../Engine/Archetypes.cs#L18). No wolves around. Declines.
- [`FeedBehavior`](../Engine/Behaviors/Feeding.cs#L43) — [`Diet.IsHungry`](../Engine/Behaviors/Feeding.cs#L33) returns false while the rabbit is
  above its hunger threshold. Declines.
- [`BreedBehavior`](../Engine/Behaviors/Breeding.cs#L15) — cooldown check, energy gate, global-cap check via
  [`Species.CountAll`](../Engine/Species.cs#L16), adjacency search. Likely declines early in the run.
- [`RestBehavior`](../Engine/Behaviors/Rest.cs#L6) — well-fed, accepts.
- [`WanderBehavior`](../Engine/Behaviors/Wander.cs#L4) — always accepts, as the priority-0 fallback.

`RestBehavior` wins on priority. Its `Act` is a no-op that returns the
baseline cost. The scheduler pushes `NextActTick` forward by
`period × cost`.

Pass 2 runs every effect on every entity. For the rabbit, that's
[`EnergyDrainEffect`](../Engine/Stats/Energy.cs#L31): one step of drain, and if Energy hits zero,
[`Health.TakeDamage`](../Engine/Stats/Health.cs#L19) bleeds a point and [`DeathHelper.DestroyEntityIfDead`](../Engine/Stats/Health.cs#L31)
decides whether to reap the corpse. For the wolf raid spawner,
[`WolfRaidEffect`](../Engine/Behaviors/WolfRaid.cs#L97) rolls its chance; usually declines, but occasionally
picks a random tree and calls [`Archetypes.CreateWolf`](../Engine/Archetypes.cs#L54) there. The new
wolf exists immediately; its [`AgilityPaced.OnAttach`](../Engine/Scheduler.cs#L43) seeds `NextActTick`
one period out, so the wolf waits a turn before its first action.

## A bite with a grapple

Later, a raid wolf has chased a rabbit across the pasture and now stands
adjacent to it. This is how the engine handles the attempted kill and,
if the rabbit survives, the pin that follows.

The wolf's turn comes up. [`HuntBehavior.WouldAct`](../Engine/Behaviors/Hunt.cs) asks
`World.GetSpeciesFlowField(CreateRabbit)` for this tick's shared flood
from every rabbit cell, then checks its 8 neighbors via
`FlowFieldHelper.PickNearestNeighborStep`. The rabbit is adjacent — one
of the wolf's neighbors IS the rabbit's cell, distance zero in the flow
field — so the behavior caches the rabbit and flags "adjacent".

[`HuntBehavior.Act`](../Engine/Behaviors/Hunt.cs#L152) reads [`Melee.Damage(wolfId, rabbitId)`](../Engine/Behaviors/Hunt.cs#L38) (attacker's
`Strength`, defender's `Toughness`), calls `TakeDamage` on the rabbit's
`Health`, and asks `DeathHelper.DestroyEntityIfDead`. If the rabbit
survived, the wolf attaches `Grappled { attackerId = wolfId }` to it.
`Act` returns its bite cost, and the wolf's scheduler pushes forward
accordingly.

On the rabbit's next turn, Pass 1 runs the grapple check.
[`Grappled.IsStillValid`](../Engine/Behaviors/Grapple.cs#L17) asks whether the wolf is still alive and still
Chebyshev-adjacent; the answer is yes, so the pin stays. The dispatcher
filters the rabbit's behaviors to those implementing
[`ICanActWhenGrappled`](../Engine/Behaviors/Grapple.cs#L45) — today only `EscapeGrappleBehavior`. It rolls
[`Grappled.EscapeChance`](../Engine/Behaviors/Grapple.cs#L33) (victim `Agility` against attacker `Strength`);
success detaches `Grappled` and steps one cell away, failure logs
"struggles but stays pinned" and burns the turn.

If the wolf had walked off before the rabbit's turn came up,
`Grappled.IsStillValid` would return false and the dispatcher would
detach `Grappled` automatically, letting the rabbit's normal behavior
list compete.

## A breeding tick

Later still, another rabbit comes up for its turn: off breeding cooldown,
Energy above the breeding gate, a same-species neighbor (also off
cooldown) adjacent.

[`BreedBehavior.WouldAct`](../Engine/Behaviors/Breeding.cs#L46) checks `Species.CountAll(CreateRabbit)` against
[`Breeding.globalCap`](../Engine/Behaviors/Breeding.cs#L11), finds the adjacent mate, rolls the breed chance,
and returns true. It wins on priority.

[`BreedBehavior.Act`](../Engine/Behaviors/Breeding.cs#L103) sets both parents' `lastBreedTick` to the current
tick and spawns a baby through the species delegate:

```csharp
int baby = species.spawn(pos.X, pos.Y);
```

`species.spawn` is `Archetypes.CreateRabbit` — calling it runs the full
archetype, creating a new integer id and attaching every component a
rabbit needs. The baby's `Breeding.lastBreedTick` gets set to the current
tick as well, so the baby is born on cooldown.

The baby is alive as soon as `spawn` returns, but it isn't in Pass 1's
iteration snapshot, so it doesn't act this tick. Its
`AgilityPaced.OnAttach` has already seeded `NextActTick` one period out
— the baby waits a full period before its first turn, same as any other
action.

Pass 2 does run `EnergyDrainEffect` on the baby this tick, though —
[`AllWithComponent<Effects>()`](../Engine/World.cs#L238) reads the *current* store and the baby is
in it. A baby's metabolism starts the moment it's born.
