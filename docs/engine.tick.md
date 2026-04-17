# The tick

`World.Tick()` is the only entry point that advances simulation. Two passes,
in order, then `tickCount++`.

## Pass 1 — Actions (pick one per entity)

For every entity with a `Behaviors` component:

1. **Is it due?** If the entity has a `Scheduler`, check
   `Scheduler.IsDue(tickCount)`. If not, skip — the entity doesn't get a turn
   this tick. If no `Scheduler`, always due (default fallback = act every tick).
2. **Ask every behavior** in the entity's list: `WouldAct(id)`. Each behavior
   may also cache target info as a side effect (which prey to bite, which
   bush to walk toward, which direction to flee).
3. **Highest-priority yes wins.** Among behaviors that returned `true`, take
   the one with the largest `Priority` value. Run `winner.Act(id)`. Exactly
   one behavior runs per entity per tick.
4. **Reschedule.** If the entity is still alive and has a `Scheduler`, call
   `Reschedule(tickCount)` — push its `nextActTick` forward by `period`.

Entities with no willing behavior do nothing that tick (e.g. `RestBehavior`
returns false for a hungry rabbit, `WanderBehavior` is always willing as a
priority-0 fallback, so something usually runs).

## Pass 2 — Effects (run all on every entity)

For every entity with an `Effects` component:

1. Iterate every effect in the list.
2. Call `effect.Apply(id)` for each.
3. If an effect destroys the host (e.g. `EnergyDrainEffect` hits 0, takes HP
   damage, kills), the inner loop breaks out of that entity's effects.

Effects run **wall-clock** — every global tick, for every entity, regardless
of `Scheduler.period`. A slow creature (period 15) still drains 1 energy per
tick, same as a fast creature (period 1). Biological aging, poison,
decay — these don't care how fast the victim moves.

## Dispatcher order

`AllWithComponent<Behaviors>()` returns a snapshot list from the underlying
dictionary. Entities spawned mid-tick (e.g. a newly-bred baby rabbit, or a
wolf emerging from a raid in Pass 2) are NOT in the snapshot, so they don't
act on the tick they were born. They're due next tick.

Iteration order is the dictionary's key order — effectively insertion order
for the typical .NET `Dictionary<int, object>`. Deterministic given a fixed
spawn order; not meaningful semantically. Two entities on the same tick see
the world state as-of-start-of-tick PLUS any changes earlier iterated
entities made (Pass 1 is sequential, not simultaneous).

## Gotchas

- **Mid-tick destruction is possible.** A behavior's `Act` (or an effect's
  `Apply`) may destroy its host or another entity. `EntityExists(id)` checks
  in the dispatcher handle this — skip if already destroyed.
- **Mid-tick creation is possible.** Breeding, raid-spawning, bush growth
  all create entities. They're in the world immediately but outside the
  current tick's iteration snapshot.
- **`AllWithComponent` returns `.ToList()`.** So mid-tick add/remove doesn't
  invalidate the iteration. Trust this guarantee; don't try to mutate
  `World`'s internal dictionaries directly.

## Why it's structured this way

- **One action per tick, structurally**: behaviors compete, only the winner
  mutates state. No behavior can "chain" into another's action the same tick.
  This removes an entire category of emergent ordering bugs.
- **Effects separate**: things that happen *to* an entity (drain, decay) are
  guaranteed to run regardless of what the entity chose to do. An entity
  can't "escape" starvation by choosing not to eat — the drain fires anyway.
