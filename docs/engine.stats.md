# Stats

**Stats** are the base attributes of a living being. They feed every
*derived* ability — bite damage, vision range, action period, escape chance.
Any rule involving "how good is this creature at X?" reduces to a formula
on stats.

## The component

```csharp
public class Stats
{
    public int Strength;
    public int Agility;
    public int Perception;
    public int Toughness;
    // Future: Mass, Intelligence, Charisma, ...
}
```

One shared schema. **Every creature with [`Stats`](../Engine/Stats/Stats.cs#L13) has every field.** Deliberate
— the distinction between "statted" (rabbits, wolves, future humans) and
"not statted" (trees, bushes, walls) is binary. A behavior that reads stats
can trust they're all there. New fields default to a low baseline so adding
a field doesn't require touching every archetype.

## Stats vs. resources

Separate concepts:
- **Stats** ([`Strength`](../Engine/Stats/Stats.cs#L15), [`Agility`](../Engine/Stats/Stats.cs#L16), ...) — static base values. Change only
  when a level-up / buff system edits them.
- **Resources** ([`Health`](../Engine/Stats/Health.cs#L7), [`Energy`](../Engine/Stats/Energy.cs#L7), future `Money`) — stateful values with
  current/max. Drain, refill, bound. They live in their own components.

If you're about to add a new field, ask: does it have a current/max? Does
it drain? Then it's a resource, not a stat.

## Derived abilities — `StatMath`

Stat-derived helpers that are generically about "reading a stat, returning
a value" live in [`Engine/Stats/StatMath.cs`](../Engine/Stats/StatMath.cs):

- [`VisionRange(id)`](../Engine/Stats/StatMath.cs#L29) — derived from `Perception`. Used wherever a creature
  asks "how far can I see?".
- [`ActionPeriod(id)`](../Engine/Stats/StatMath.cs#L39) — derived from `Agility`. Higher Agility means a lower
  period means faster action. Used by [`AgilityPaced.Reschedule`](../Engine/Scheduler.cs#L60).

See [`StatMath.cs`](../Engine/Stats/StatMath.cs) for the exact formulas; they are plain arithmetic on one
stat each.

**Capability-specific formulas live on the capability's component.** Not
in `StatMath`:

- [`Melee.Damage(atkId, defId)`](../Engine/Behaviors/Hunt.cs#L38) reads the attacker's `Strength` and the
  defender's `Toughness`. The [`Melee`](../Engine/Behaviors/Hunt.cs#L29) component owns "how hard does my
  strike land?" because it's a property of the attacker-vs-defender
  pairing. Toughness lives here, not in [`Health.TakeDamage`](../Engine/Stats/Health.cs#L19), so internal
  damage sources (starvation) aren't soaked.
- [`Grappled.EscapeChance(victimId)`](../Engine/Behaviors/Grapple.cs#L33) reads the victim's `Agility` against
  the attacker's `Strength`. The [`Grappled`](../Engine/Behaviors/Grapple.cs#L8) state owns the escape formula
  because it's an interaction between specifically-named attacker and
  victim entities.

If a `Weapon` component later stacks on top, `Melee.Damage` composes:
attacker `Strength` plus the weapon's bonus.

To tune game feel, change the constants in `StatMath` or the relevant
component. No scatter-fix across behaviors.

Once `StatMath` grows past a handful of methods or obvious domain splits,
extract `CombatMath` / `MovementMath` / `PerceptionMath`. Not before.

## Enforcement

Every `StatMath` method starts with [`Require(id)`](../Engine/Stats/StatMath.cs#L17) — fetch Stats, throw a
descriptive `InvalidOperationException` if absent. Any stat-dependent code
path funnels through this helper, so an archetype that forgets to attach
`Stats` to a creature fails loudly the first time that creature tries to
read a stat.

No marker interface, no dispatcher check — one mechanism, one place to
look. A reader asking "does this behavior need Stats?" answers the
question by grep: if the behavior calls a `StatMath` method, it needs
Stats.

## When to add a new stat

Add a new stat when a behavior needs it — not before. Speculative
"let's add Charisma because maybe someday…" invites drift. Add the stat
the tick some behavior would use it; give it a low default on the
`Stats` class so existing archetypes don't break.

## Related

- [`docs/engine.scheduler.md`](engine.scheduler.md) — how `AgilityPaced` uses `ActionPeriod`.
- [`docs/engine.movement.md`](engine.movement.md) — how `VisionRange` gates flow-field neighbor picks.
