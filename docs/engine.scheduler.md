# Scheduler

A scheduler paces an entity's `Behaviors` dispatch. Effects run on a
different schedule — wall-clock, every tick, regardless of pace (see
`engine.tick.md`).

Two concrete scheduler types, picked per archetype to make the statted /
simpleton split visible in code:

- **`AgilityPaced`** — for statted creatures. Period comes from
  `Stats.Agility` via `StatMath.ActionPeriod(id)`, recomputed at each
  reschedule. Buffs to Agility take effect on the very next action.
- **`FixedPaced`** — for simpletons (bushes, future grass, door tickers).
  Period is a literal field, set at archetype time.

```csharp
public interface IScheduler
{
    int NextActTick { get; set; }
    bool IsDue(int globalTick);
    void Reschedule(int globalTick, int cost, int entityId);
}

public class AgilityPaced : IScheduler { ... }           // period from Stats.Agility
public class FixedPaced   : IScheduler { public int period; ... }
```

`Scheduling.Get(id)` returns whichever is attached, or `null` if the
entity isn't on any schedule (terrain, corpses, event spawners).

## How the dispatcher uses it

In Pass 1 of `World.Tick`, for each entity with `Behaviors`:

```csharp
IScheduler? sched = Scheduling.Get(id);
if (sched != null && !sched.IsDue(tickCount))
    continue;                                         // skip — not due yet

// ... pick and run winning behavior, capture cost ...

if (EntityExists(id) && sched != null)
    sched.Reschedule(tickCount, cost, id);
```

Entities without *any* scheduler fall back to "act every tick" — rare in
practice (terrain doesn't have `Behaviors`).

## Interaction with Effects

Effects in Pass 2 run on every entity with `Effects`, regardless of
pace. A slow creature drains the same energy per global tick as a fast
one; poison, aging, and bleeding work the same way.

One consequence to know for tuning: slower entities need larger
`Energy` pools to survive the same wall-clock lifespan as faster ones.

## Baby entities

When a scheduler is attached, its `OnAttach` hook sets
`NextActTick = World.tickCount + period`. So a newly spawned entity
(breeding baby, raid wolf, vegetation sprout) waits one full period
before its first action — same pacing as any later action. Without this,
`NextActTick` would default to `0`, and any mid-game spawn would get a
free action on the very next tick regardless of how slow it is.

`AgilityPaced.OnAttach` reads the period via `StatMath.ActionPeriod(id)`,
which requires `Stats` to already be on the entity — archetypes attach
`Stats` before `AgilityPaced` for this reason.

## Varied action cost

Not every action takes the same wall-clock time. `IBehavior.Act` returns
an `int` cost; `Reschedule` multiplies by it when pushing `NextActTick`
forward:

```
NextActTick = globalTick + period * cost;
```

A step is the baseline. An action that takes longer in fiction (biting,
mating, eating) returns a larger cost, which delays the creature's next
turn proportionally.

**Cost is dynamic per-action, not a property of the behavior.** The same
`HuntBehavior` returns a small cost for a step and a larger cost for a
bite. Return the cost from `Act`; don't declare it as a static property.

For the current cost per action, read each behavior's `Act` — the number
is right there at the `return`. Tuning lives in code, not here.
