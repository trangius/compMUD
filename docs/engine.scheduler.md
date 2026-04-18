# Scheduler

`Scheduler` paces an entity's `Behaviors` dispatch. It does NOT affect
`Effects` — those are wall-clock (see `engine.tick.md`).

## The component

```csharp
public class Scheduler
{
    public int period = 1;       // global ticks between actions; must be >= 1
    public int nextActTick = 0;  // tick value at which this entity next acts

    public bool IsDue(int globalTick);
    public void Reschedule(int globalTick, int cost = 1);  // nextActTick = globalTick + period * cost
}
```

## How it gates a turn

In Pass 1 of `World.Tick`, for each entity with `Behaviors`:

```
if (HasComponent<Scheduler>(id) && !GetComponent<Scheduler>(id).IsDue(tickCount))
    continue;                                         // skip — not due yet

// Run winning behavior — its Act returns a cost (1 = baseline, higher = slower)
int cost = winner?.Act(id) ?? 1;

if (EntityExists(id) && HasComponent<Scheduler>(id))
    GetComponent<Scheduler>(id).Reschedule(tickCount, cost);
```

An entity without a `Scheduler` falls back to "act every tick" — the default
and the legacy behavior from before the scheduler existed.

## Current pace assignments

| Archetype | period | Acts every N ticks |
|---|---|---|
| Wolf (raider) | 10 | 10 |
| Rabbit | 15 | 15 |
| Bush | 30 | 30 |
| Raid spawner | n/a (no Behaviors — it's purely an Effect host) | — |

"1 is lightspeed" — any entity with `period = 1` acts on every global tick,
the fastest possible pace. Leave room below normal creatures for spells /
buffs that bump something temporarily to period 1-3.

## Interaction with Effects

`EnergyDrainEffect` runs in Pass 2 for every entity with Effects, regardless
of `Scheduler`. A rabbit (period 15) drains 1 energy per global tick — 15
energy per rabbit-action — same as a wolf (period 10) drains 10 per action.
This is intentional: metabolism doesn't know or care about action speed.
Poison, aging, bleeding all work the same way.

Consequence: slower entities need larger `Energy` pools to stay viable for
the same wall-clock lifespan. The current tuning scales accordingly (wolf
`Energy(1000)`, rabbit `Energy(1500)`).

## Baby entities

When a new entity is spawned mid-tick (breeding baby, raid wolf, vegetation
sprout), its `Scheduler.nextActTick` defaults to 0. On the *next* global tick,
`tickCount >= 0` so it's immediately due. Babies act one tick after their
birth tick.

## Varied action cost

Not every action takes the same wall-clock time. `IBehavior.Act` returns an
`int` cost (default 1 = baseline); `Reschedule` multiplies by that cost when
pushing `nextActTick` forward:

```csharp
nextActTick = globalTick + period * cost;
```

A wolf's step costs 1 period (10 ticks); a wolf's bite costs 2 (20 ticks —
the predator pauses to commit to the attack). A rabbit walking toward a mate
costs 1 period (15 ticks); the actual mating act costs 3 (45 ticks — a bigger
pause, matches the biological weight).

**Cost is dynamic per-action, not a property of the behavior.** The same
`HuntBehavior` returns 1 when it stepped and 2 when it bit. Return cost from
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

Tune these as the fiction grows — casting a spell might cost 5, a quick dodge
might cost less than baseline (but no sub-1 mechanism exists yet; add one
only if needed).
