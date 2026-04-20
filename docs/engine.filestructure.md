# Engine file structure

```
Engine/
  World.cs            State stores + World.Tick (the two-pass dispatcher) + cell queries.
                      Single static class — nothing ever does `new World()`.
  Archetypes.cs       "What is a rabbit?" — factory functions that compose components
                      onto a new entity. CreateRabbit, CreateWolf, CreateBush, etc.
  EntityInfo.cs       Named, Appearance — identity/display metadata.
  Species.cs          Species component + CountAll / CountInRadius helpers.
                      Species identity = the spawn delegate (see engine.species.md).
  Tree.cs             Tree marker State (raiding wolves spawn at / retreat to one).
  Yields.cs           Yield, Yields (latent multi-item production on corpses,
                      bushes, trees) + ResourceCategory + Resources registry.
                      Cross-cutting — used by feeding today, butchering/chopping later.
  Scheduler.cs        IScheduler interface + AgilityPaced (stat-driven) +
                      FixedPaced (literal period) + Scheduling.Get helper.
                      See engine.scheduler.md.

  Areas/              One file per concrete area. The engine doesn't know about areas;
                      each file is a self-contained `StartingArea()`-style builder.
    HomeArea.cs       The default area — walled pasture, pond, NW-corner forest,
                      rabbits, and a wolf raid spawner.

  Spatial/            Position, passability, pathfinding.
    Spatial.cs        Position, Walkable, Solid (marker States) + MovementHelper
                      (TryMove, MoveToward, MoveAwayFrom, Wander — all 8-connected).
    Algorithms.cs     Generic grid BFS + BFSResult (distance, FirstStep). Used by
                      Feed, Hunt, and ReturnToForest.

  Stats/              Static attributes + resources.
    Stats.cs          Stats (Strength, Agility, Perception, Toughness).
    StatMath.cs       Stat-reading helpers (VisionRange, ActionPeriod) +
                      Require. Interaction-specific formulas (Melee.Damage,
                      Grappled.EscapeChance) live with their components.
    Health.cs         Health (resource), Corpse marker, DeathHelper.
    Energy.cs         Energy (resource) + EnergyDrainEffect.

  Behaviors/          Each file holds: marker State(s) + the Behavior that keys off them.
                      See engine.five-buckets.md for the pattern.
    Behavior.cs       IBehavior interface + Behaviors container component (pick one).
    Hunt.cs           Predator { preySpecies: HashSet<spawn> }, Melee (owns
                      bite-damage formula), HuntBehavior.
    Grapple.cs        Grappled state { attackerId, IsStillValid, EscapeChance },
                      ICanActWhenGrappled marker interface, EscapeGrappleBehavior.
    RunFromPredator.cs
                      RunFromPredatorBehavior — AI reflex when a predator is in sight.
                      No separate marker — species membership via Predator.preySpecies does it.
    Wander.cs         WanderBehavior (random step fallback).
    Rest.cs           RestBehavior (fed entities sit still).
    Feeding.cs        Diet, FeedBehavior. The "eating" feature — Yields and
                      ResourceCategory are the primitives it drains; they live
                      in Engine/Yields.cs because they're not feeding-specific.
    Breeding.cs       Breeding, BreedBehavior (species-matching via Species.spawn).
    Vegetation.cs     Vegetation, GrowBehavior (plants spread to neighbor cells).
    WolfRaid.cs       RaidingWolf, ReturnToForestBehavior, WolfRaidEffect.
                      A wolf raid = spawn → kill → retreat → despawn.

  Effects/            Passive per-tick updates (run every tick on every host).
    Effect.cs         IEffect interface + Effects container component (run all).

Console/Program.cs    Text frontend — see docs/console.readme.md.
Gui/Game1.cs          MonoGame frontend — see docs/gui.readme.md.
```

## File-per-feature principle

A `State` marker lives in the same file as the `Behavior` or `Effect` that
reads it. `Hunt.cs` holds `Predator` (the marker that lists prey species),
`Melee` (the component that owns the bite-damage formula), and
`HuntBehavior` (what ties them together on a turn). One file, one feature.

Exceptions are cross-cutting State components that no single behavior owns
(e.g. `Species`, `Position`, the schedulers `AgilityPaced` and `FixedPaced`)
— those live in their own files or in shared infrastructure files.
