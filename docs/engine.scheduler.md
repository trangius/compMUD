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
    public void Reschedule(int globalTick);  // nextActTick = globalTick + period
}
```

## How it gates a turn

In Pass 1 of `World.Tick`, for each entity with `Behaviors`:

```
if (HasComponent<Scheduler>(id) && !GetComponent<Scheduler>(id).IsDue(tickCount))
    continue;                                  // skip — not due yet

// run winning behavior
winner?.Act(id);

if (EntityExists(id) && HasComponent<Scheduler>(id))
    GetComponent<Scheduler>(id).Reschedule(tickCount);
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

## What's planned but not in yet

- **Varied action cost.** `Act()` would return a cost; `Reschedule` becomes
  `nextActTick = globalTick + period * cost`. Bite = 2, step = 1, long
  incantation = 5. Documented design; no code change yet. See
  `README.md` (in-flight section) for intent.
