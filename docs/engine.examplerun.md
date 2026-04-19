# Example run

A trace of what happens when `Console/Program.cs` starts and advances a
few ticks — the view from inside the engine as specific scenes play out.

## Startup

`Program.cs` calls `World.Initialize(width, height)`, then
`HomeArea.StartingArea()`, then enters the REPL.

`Initialize` just assigns `mapWidth` and `mapHeight` — two field writes,
no entities.

`StartingArea` builds the world in stages: border walls, interior grass,
a pond carved out of grass by destroying the grass entities in an ellipse
and creating water entities in their place, trees scattered (dense NW,
thin elsewhere), bushes, rabbits, and the wolf raid spawner.

Each archetype call creates a new entity (`World.CreateEntity()` returns
a fresh integer id) and attaches components to it. The first `Position`
attach registers the entity in the spatial index at its cell.
`DestroyEntity` walks every component store, fires `OnDetach` on anything
that implements it (removing from the spatial index is one such hook),
then drops the id.

After `StartingArea`, the world holds thousands of integer ids. Most are
terrain; the `Behaviors` store has one entry per rabbit and bush; the
`Effects` store has one per rabbit plus the wolf raid spawner (which
carries only `Effects`, no `Behaviors`).

## A quiet tick

The REPL calls `World.Tick()`. Pass 1 iterates every entity with
`Behaviors`. Take a well-fed rabbit with no wolves in sight.

Its `AgilityPaced` scheduler reports it as due. It carries no `Grappled`
component. The dispatcher walks its behavior list and asks each
`WouldAct`:

- `EscapeGrappleBehavior` — not grappled. Declines.
- `RunFromPredatorBehavior` — `FindNearestEntity` in a Euclidean disk
  sized by `Stats.Perception`, filtered for any `Predator` whose
  `preySpecies` contains `CreateRabbit`. No wolves around. Declines.
- `HarvestBehavior` and `FeedBehavior` — `Diet.IsHungry` returns false
  while the rabbit is above its hunger threshold. Both decline.
- `BreedBehavior` — cooldown check, energy gate, global-cap check via
  `Species.CountAll`, adjacency search. Likely declines early in the run.
- `RestBehavior` — well-fed, accepts.
- `WanderBehavior` — always accepts, as the priority-0 fallback.

`RestBehavior` wins on priority. Its `Act` is a no-op that returns the
baseline cost. The scheduler pushes `NextActTick` forward by
`period × cost`.

Pass 2 runs every effect on every entity. For the rabbit, that's
`EnergyDrainEffect`: one step of drain, and if Energy hits zero,
`Health.TakeDamage` bleeds a point and `DeathHelper.DestroyEntityIfDead`
decides whether to reap the corpse. For the wolf raid spawner,
`WolfRaidEffect` rolls its chance; usually declines, but occasionally
picks a random tree and calls `Archetypes.CreateWolf` there. The new
wolf exists immediately; its `AgilityPaced.OnAttach` seeds `NextActTick`
one period out, so the wolf waits a turn before its first action.

## A bite with a grapple

Later, a raid wolf has chased a rabbit across the pasture and now stands
adjacent to it. This is how the engine handles the attempted kill and,
if the rabbit survives, the pin that follows.

The wolf's turn comes up. `HuntBehavior.WouldAct` floods the reachable
cells with BFS out to `Stats.Perception`, scans every `Species` holder
the flood touched, keeps those on the wolf's `preySpecies` set, and picks
the nearest. The rabbit is adjacent — the wolf's own cell is a neighbor
of the rabbit's, BFS distance zero — so the behavior caches the rabbit
and flags "adjacent".

`HuntBehavior.Act` reads `Melee.Damage(wolfId, rabbitId)` (attacker's
`Strength`, defender's `Toughness`), calls `TakeDamage` on the rabbit's
`Health`, and asks `DeathHelper.DestroyEntityIfDead`. If the rabbit
survived, the wolf attaches `Grappled { attackerId = wolfId }` to it.
`Act` returns its bite cost, and the wolf's scheduler pushes forward
accordingly.

On the rabbit's next turn, Pass 1 runs the grapple check.
`Grappled.IsStillValid` asks whether the wolf is still alive and still
Chebyshev-adjacent; the answer is yes, so the pin stays. The dispatcher
filters the rabbit's behaviors to those implementing
`ICanActWhenGrappled` — today only `EscapeGrappleBehavior`. It rolls
`Grappled.EscapeChance` (victim `Agility` against attacker `Strength`);
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

`BreedBehavior.WouldAct` checks `Species.CountAll(CreateRabbit)` against
`Breeding.globalCap`, finds the adjacent mate, rolls the breed chance,
and returns true. It wins on priority.

`BreedBehavior.Act` sets both parents' `lastBreedTick` to the current
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
`AllWithComponent<Effects>()` reads the *current* store and the baby is
in it. A baby's metabolism starts the moment it's born.
