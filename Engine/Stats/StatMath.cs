namespace Engine;

// ----------------------------------------------------------------------------
// All stat-derived formulas live here. Bite damage, vision range, action
// period, escape chance — each is a single static method, one formula, one
// line of magic numbers. Tuning the game's feel happens in this file and
// nowhere else. When we need a second helper class (CombatMath, MovementMath),
// split by domain then — not before.
// ----------------------------------------------------------------------------
public static class StatMath
{
    // ----------------------------------------------------------------------------
    // Fetch an entity's Stats, throwing a descriptive error if absent. Every
    // StatMath method calls this first — any stat-dependent code path fails
    // loudly if an archetype forgot to attach Stats to a creature.
    // ----------------------------------------------------------------------------
    public static Stats Require(int id)
    {
        if (!World.HasComponent<Stats>(id))
            throw new InvalidOperationException(
                $"Entity {id} expected a Stats component but has none.");
        return World.GetComponent<Stats>(id);
    }

    // ----------------------------------------------------------------------------
    // Perception → grid cells the entity can sense. 1:1 mapping keeps the
    // mental model simple: Perception 15 = sees 15 cells away.
    // ----------------------------------------------------------------------------
    public static int VisionRange(int id)
    {
        return Require(id).Perception;
    }

    // ----------------------------------------------------------------------------
    // Ticks between actions, derived from Agility. Higher Agility → lower
    // period → faster. Agility 85 yields period 0 — clamp to 1 so nothing
    // ticks every sub-tick.
    // ----------------------------------------------------------------------------
    public static int ActionPeriod(int id)
    {
        return Math.Max(1, 85 - Require(id).Agility);
    }
}
