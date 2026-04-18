# Stats

**Stats** are the base attributes of a living being. They feed every
*derived* ability — bite damage, vision range, action period, escape chance.
Any rule involving "how good is this creature at X?" eventually reduces to a
formula on stats.

## The component

```csharp
public class Stats
{
    public int Strength = 1;
    public int Agility = 1;
    public int Perception = 1;
    // Future: Toughness, Mass, Intelligence, Charisma, ...
}
```

One shared schema. **Every creature with `Stats` has every field.** Deliberate
— the distinction between "statted" (rabbits, wolves, future humans) and
"not statted" (trees, bushes, walls) is binary. A behavior that reads stats
can trust they're all there.

Scale convention: **1–100**, Dark-Souls-style. Defaults at 1 so adding a new
field here doesn't require touching every archetype.

## Stats vs. resources

Separate concepts:
- **Stats** (`Strength`, `Agility`, ...) — static base values. Change only
  when a level-up / buff system edits them.
- **Resources** (`Health`, `Energy`, future `Money`) — stateful values with
  current/max. Drain, refill, bound. They live in their own components.

If you're about to add a new field, ask: does it have a current/max? Does it
drain? Then it's a resource, not a stat.

## Derived abilities — `StatMath`

Stat-derived formulas that are generically about "reading a stat, returning
a value" live in `Engine/Stats/StatMath.cs`:

| Method | Formula | Notes |
|---|---|---|
| `VisionRange(id)` | `Perception` | 1:1 → grid cells. Clear mental model. |
| `ActionPeriod(id)` | `max(1, 85 - Agility)` | Higher Agility → faster. Floor at 1. |

**Capability-specific formulas live on the capability's component.** Examples:

- `Melee.Damage(id)` = `max(1, Strength / 25)` — the `Melee` component owns
  "how hard does my strike hit?" because it's a property of being a melee
  attacker, not of being statted in general.
- `Grappled.EscapeChance(victimId)` = `victimAgi / (victimAgi + 3 * attackerStr)`
  — the `Grappled` state owns the escape formula because it's an interaction
  between specifically-named attacker and victim entities.

If a `Weapon` component later stacks on top, `Melee.Damage` composes:
`Strength + weapon.bonus`. Same for any future interaction-specific formula.

To tune game feel, change the constants in `StatMath` or the relevant
component. No scatter-fix across behaviors.

Once `StatMath` has 6+ methods or obvious domain splits, extract
`CombatMath` / `MovementMath` / `PerceptionMath`. Not before.

## Enforcement

Every `StatMath` method starts with `Require(id)` — fetch Stats, throw a
descriptive `InvalidOperationException` if absent. Any stat-dependent code
path funnels through this helper, so an archetype that forgets to attach
`Stats` to a creature fails loudly the first time that creature tries to
read a stat.

No marker interface, no dispatcher check — one mechanism, one place to
look. A reader asking "does this behavior need Stats?" answers the question
by grep: if the behavior calls a `StatMath` method, it needs Stats.

## Scale tuning in practice

Current archetype values (for reference):

| Creature | Strength | Agility | Perception | Derived |
|---|---|---|---|---|
| Wolf | 80 | 75 | 100 | bite 3, period 10, vision 100 |
| Rabbit | 10 | 70 | 15 | bite 1, period 15, vision 15 |

The 1–100 scale leaves room for weaker (mouse: Agility 50?) and stronger
(bear: Strength 120? — or cap at 100, adjust formulas). No hard ceiling
enforced yet.

## When to add a new stat

When a behavior needs one. Not before. Speculative "let's add Charisma
because maybe someday…" invites drift. Add it the tick some behavior would
use it; default it to 1 on the `Stats` class so existing archetypes don't
break.

## Related

- `docs/engine.scheduler.md` — how `AgilityPaced` uses `ActionPeriod`.
- `docs/engine.movement.md` — how `VisionRange` drives BFS range.
- `todo.stats.md` — longer-term plans (effective stats for items, buff system).
