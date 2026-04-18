# Scheduler

A scheduler paces an entity's `Behaviors` dispatch. It does NOT affect
`Effects` — those are wall-clock (see `engine.tick.md`).

Two concrete scheduler types, both implementing the same interface — the
statted / simpleton split is visible at the archetype:

- **`AgilityPaced`** — for statted creatures. Period comes from `Stats.Agility`
  via `StatMath.ActionPeriod(id)`, recomputed at each reschedule. Buffs to
  Agility take effect on the very next action.
- **`FixedPaced`** — for simpletons (bushes, future grass, door tickers).
  Period is a literal field.

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

`Scheduling.Get(id)` returns whichever is attached, or `null` if the entity
isn't on any schedule (terrain, corpses, event spawners).

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
practice (terrain doesn't have Behaviors).

## Current pace assignments

| Archetype | Scheduler | Period | Acts every N ticks |
|---|---|---|---|
| Wolf | `AgilityPaced` | `85 - Agi(75) = 10` | 10 |
| Rabbit | `AgilityPaced` | `85 - Agi(70) = 15` | 15 |
| Bush | `FixedPaced` | literal 30 | 30 |
| Raid spawner | none (has only Effects, not Behaviors) | — | — |

"1 is lightspeed" — Agility ≥ 84 (or `FixedPaced.period = 1`) gives a creature
the fastest possible pace, acting on every global tick.

## Interaction with Effects

`EnergyDrainEffect` runs in Pass 2 for every entity with Effects, regardless
of pace. A rabbit (period 15) drains 1 energy per global tick — 15 energy
per rabbit-action — same as a wolf (period 10) drains 10 per action. This is
intentional: metabolism doesn't know or care about action speed. Poison,
aging, bleeding all work the same way.

Consequence: slower entities need larger `Energy` pools to stay viable for
the same wall-clock lifespan. The current tuning scales accordingly.

## Baby entities

When a scheduler is attached, its `OnAttach` hook sets
`NextActTick = World.tickCount + period`. So a newly spawned entity (breeding
baby, raid wolf, vegetation sprout) waits one full period before its first
action — same pacing as any later action. Without this the scheduler would
default `NextActTick` to 0, and any mid-game spawn would get a free action on
the very next tick regardless of how slow it is.

`AgilityPaced.OnAttach` reads the period via `StatMath.ActionPeriod(id)`,
which requires `Stats` to already be on the entity — archetypes attach
`Stats` before `AgilityPaced` for this reason.

## Varied action cost

Not every action takes the same wall-clock time. `IBehavior.Act` returns an
`int` cost (default 1 = baseline); `Reschedule` multiplies by that cost when
pushing `NextActTick` forward:

```
NextActTick = globalTick + period * cost;
```

A wolf's step costs 1 period (10 ticks); a wolf's bite costs 3. A rabbit's
step costs 1 period (15 ticks); mating costs 8.

**Cost is dynamic per-action, not a property of the behavior.** The same
`HuntBehavior` returns 1 when it stepped and 3 when it bit. Return cost from
`Act`, not from a static property.

### Current cost table

| Behavior | Action | Cost |
|---|---|---|
| `RunFromPredatorBehavior` | step | 1 |
| `WanderBehavior` | step | 1 |
| `RestBehavior` | no-op | 1 |
| `FeedBehavior` | walk toward food | 1 |
| `FeedBehavior` | **eat (consume underfoot item)** | **5** |
| `HarvestBehavior` | harvest bush | 1 |
| `GrowBehavior` | spread / spawn | 1 |
| `ReturnToForestBehavior` | step / vanish | 1 |
| `BreedBehavior` | walk toward mate | 1 |
| `BreedBehavior` | **mate (produce baby)** | **8** |
| `HuntBehavior` | walk toward prey | 1 |
| `HuntBehavior` | **bite** | **3** |
| `EscapeGrappleBehavior` | struggle / break free | 1 |

Tune these as the fiction grows.
