# Engine file structure

```
Engine/
```
- [`World.cs`](../Engine/World.cs) — State stores + [`World.Tick`](../Engine/World.cs#L67) (the two-pass dispatcher) + cell queries. Single static class — nothing ever does `new World()`.
- [`Archetypes.cs`](../Engine/Archetypes.cs) — "What is a rabbit?" — factory functions that compose components onto a new entity. [`CreateRabbit`](../Engine/Archetypes.cs#L18), [`CreateWolf`](../Engine/Archetypes.cs#L54), [`CreateBush`](../Engine/Archetypes.cs#L106), etc.
- [`EntityInfo.cs`](../Engine/EntityInfo.cs) — [`Named`](../Engine/EntityInfo.cs#L4), [`Appearance`](../Engine/EntityInfo.cs#L11) — identity/display metadata.
- [`Species.cs`](../Engine/Species.cs) — [`Species`](../Engine/Species.cs#L8) component + [`CountAll`](../Engine/Species.cs#L16) / [`CountInRadius`](../Engine/Species.cs#L32) helpers. Species identity = the spawn delegate (see [engine.species.md](engine.species.md)).
- [`Tree.cs`](../Engine/Tree.cs) — [`Tree`](../Engine/Tree.cs#L5) marker State (raiding wolves spawn at / retreat to one).
- [`Yields.cs`](../Engine/Yields.cs) — [`Yield`](../Engine/Yields.cs#L33), [`Yields`](../Engine/Yields.cs#L52) (latent multi-item production on corpses, bushes, trees) + [`ResourceCategory`](../Engine/Yields.cs#L10) + [`Resources`](../Engine/Yields.cs#L21) registry. Cross-cutting — used by feeding today, butchering/chopping later.
- [`Scheduler.cs`](../Engine/Scheduler.cs) — [`IScheduler`](../Engine/Scheduler.cs#L24) interface + [`AgilityPaced`](../Engine/Scheduler.cs#L33) (stat-driven) + [`FixedPaced`](../Engine/Scheduler.cs#L68) (literal period) + [`Scheduling.Get`](../Engine/Scheduler.cs#L99) helper. See [engine.scheduler.md](engine.scheduler.md).

```
  Areas/
```
- [`HomeArea.cs`](../Engine/Areas/HomeArea.cs) — The default area — walled pasture, pond, NW-corner forest, rabbits, and a wolf raid spawner. Self-contained [`StartingArea()`](../Engine/Areas/HomeArea.cs#L14) builder.

```
  Spatial/
```
- [`Spatial.cs`](../Engine/Spatial/Spatial.cs) — [`Position`](../Engine/Spatial/Spatial.cs#L9), [`Walkable`](../Engine/Spatial/Spatial.cs#L25), [`Solid`](../Engine/Spatial/Spatial.cs#L29) (marker States) + [`MovementHelper`](../Engine/Spatial/Spatial.cs#L34) ([`TryMove`](../Engine/Spatial/Spatial.cs#L48), [`MoveToward`](../Engine/Spatial/Spatial.cs#L81), [`MoveAwayFrom`](../Engine/Spatial/Spatial.cs#L113), [`Wander`](../Engine/Spatial/Spatial.cs#L151) — all 8-connected).
- [`Algorithms.cs`](../Engine/Spatial/Algorithms.cs) — Generic grid [`BFS`](../Engine/Spatial/Algorithms.cs#L28) + [`BFSResult`](../Engine/Spatial/Algorithms.cs#L81) (distance, FirstStep). Used by Feed, Hunt, and ReturnToForest.

```
  Stats/
```
- [`Stats.cs`](../Engine/Stats/Stats.cs) — [`Stats`](../Engine/Stats/Stats.cs#L13) (Strength, Agility, Perception, Toughness).
- [`StatMath.cs`](../Engine/Stats/StatMath.cs) — Stat-reading helpers ([`VisionRange`](../Engine/Stats/StatMath.cs#L29), [`ActionPeriod`](../Engine/Stats/StatMath.cs#L39)) + [`Require`](../Engine/Stats/StatMath.cs#L17). Interaction-specific formulas ([`Melee.Damage`](../Engine/Behaviors/Hunt.cs#L38), [`Grappled.EscapeChance`](../Engine/Behaviors/Grapple.cs#L33)) live with their components.
- [`Health.cs`](../Engine/Stats/Health.cs) — [`Health`](../Engine/Stats/Health.cs#L7) (resource), [`Corpse`](../Engine/Stats/Health.cs#L24) marker, [`DeathHelper`](../Engine/Stats/Health.cs#L29).
- [`Energy.cs`](../Engine/Stats/Energy.cs) — [`Energy`](../Engine/Stats/Energy.cs#L7) (resource) + [`EnergyDrainEffect`](../Engine/Stats/Energy.cs#L31).

```
  Behaviors/          Each file holds: marker State(s) + the Behavior that keys off them.
```
See [engine.five-buckets.md](engine.five-buckets.md) for the pattern.

- [`Behavior.cs`](../Engine/Behaviors/Behavior.cs) — [`IBehavior`](../Engine/Behaviors/Behavior.cs#L9) interface + [`Behaviors`](../Engine/Behaviors/Behavior.cs#L28) container component (pick one).
- [`Hunt.cs`](../Engine/Behaviors/Hunt.cs) — [`Predator`](../Engine/Behaviors/Hunt.cs#L7) `{ preySpecies: HashSet<spawn> }`, [`Melee`](../Engine/Behaviors/Hunt.cs#L29) (owns bite-damage formula), [`HuntBehavior`](../Engine/Behaviors/Hunt.cs#L60).
- [`Grapple.cs`](../Engine/Behaviors/Grapple.cs) — [`Grappled`](../Engine/Behaviors/Grapple.cs#L8) state `{ attackerId, IsStillValid, EscapeChance }`, [`ICanActWhenGrappled`](../Engine/Behaviors/Grapple.cs#L45) marker interface, [`EscapeGrappleBehavior`](../Engine/Behaviors/Grapple.cs#L53).
- [`RunFromPredator.cs`](../Engine/Behaviors/RunFromPredator.cs) — [`RunFromPredatorBehavior`](../Engine/Behaviors/RunFromPredator.cs#L9) — AI reflex when a predator is in sight. No separate marker — species membership via `Predator.preySpecies` does it.
- [`Wander.cs`](../Engine/Behaviors/Wander.cs) — [`WanderBehavior`](../Engine/Behaviors/Wander.cs#L4) (random step fallback).
- [`Rest.cs`](../Engine/Behaviors/Rest.cs) — [`RestBehavior`](../Engine/Behaviors/Rest.cs#L6) (fed entities sit still).
- [`Feeding.cs`](../Engine/Behaviors/Feeding.cs) — [`Diet`](../Engine/Behaviors/Feeding.cs#L11), [`FeedBehavior`](../Engine/Behaviors/Feeding.cs#L43). The "eating" feature — `Yields` and `ResourceCategory` are the primitives it drains; they live in [`Engine/Yields.cs`](../Engine/Yields.cs) because they're not feeding-specific.
- [`Breeding.cs`](../Engine/Behaviors/Breeding.cs) — [`Breeding`](../Engine/Behaviors/Breeding.cs#L6), [`BreedBehavior`](../Engine/Behaviors/Breeding.cs#L15) (species-matching via `Species.spawn`).
- [`Vegetation.cs`](../Engine/Behaviors/Vegetation.cs) — [`Vegetation`](../Engine/Behaviors/Vegetation.cs#L8), [`GrowBehavior`](../Engine/Behaviors/Vegetation.cs#L17) (plants spread to neighbor cells).
- [`WolfRaid.cs`](../Engine/Behaviors/WolfRaid.cs) — [`RaidingWolf`](../Engine/Behaviors/WolfRaid.cs#L6), [`ReturnToForestBehavior`](../Engine/Behaviors/WolfRaid.cs#L17), [`WolfRaidEffect`](../Engine/Behaviors/WolfRaid.cs#L97). A wolf raid = spawn → kill → retreat → despawn.

```
  Effects/            Passive per-tick updates (run every tick on every host).
```
- [`Effect.cs`](../Engine/Effects/Effect.cs) — [`IEffect`](../Engine/Effects/Effect.cs#L10) interface + [`Effects`](../Engine/Effects/Effect.cs#L16) container component (run all).

[`Console/Program.cs`](../Console/Program.cs) — Text frontend — see [docs/console.readme.md](console.readme.md).
[`Gui/Game1.cs`](../Gui/Game1.cs) — MonoGame frontend — see [docs/gui.readme.md](gui.readme.md).

## File-per-feature principle

A `State` marker lives in the same file as the `Behavior` or `Effect` that
reads it. [`Hunt.cs`](../Engine/Behaviors/Hunt.cs) holds `Predator` (the marker that lists prey species),
`Melee` (the component that owns the bite-damage formula), and
`HuntBehavior` (what ties them together on a turn). One file, one feature.

Exceptions are cross-cutting State components that no single behavior owns
(e.g. `Species`, `Position`, the schedulers `AgilityPaced` and `FixedPaced`)
— those live in their own files or in shared infrastructure files.
