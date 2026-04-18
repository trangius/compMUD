namespace Engine;

// ----------------------------------------------------------------------------
// Two flavors of scheduler, picked per archetype to make the statted /
// simpleton split visible in code:
//
//   AgilityPaced — for statted creatures. Period is derived from Stats.Agility
//                  each time, so buffs to Agility immediately affect pacing.
//                  No period field.
//
//   FixedPaced   — for simpletons (bush, future grass, door tickers). Period
//                  is a literal field. Doesn't require Stats on the entity.
//
// Both expose IScheduler so the dispatcher treats them uniformly. Scheduling.Get
// finds whichever one an entity has attached.
// ----------------------------------------------------------------------------

public interface IScheduler
{
    int NextActTick { get; set; }
    bool IsDue(int globalTick);
    void Reschedule(int globalTick, int cost, int entityId);
}

// State: stat-driven scheduler. Period = StatMath.ActionPeriod(id).
// Requires Stats on the entity — dispatcher + StatMath will throw if missing.
public class AgilityPaced : IScheduler
{
    public int NextActTick { get; set; }

    // ----------------------------------------------------------------------------
    // Due if the global tick has caught up to our scheduled next action.
    // ----------------------------------------------------------------------------
    public bool IsDue(int globalTick)
    {
        return globalTick >= NextActTick;
    }

    // ----------------------------------------------------------------------------
    // Push forward by (period × cost), where period comes fresh from Stats.
    // No caching — changes to Agility take effect on the very next action.
    // ----------------------------------------------------------------------------
    public void Reschedule(int globalTick, int cost, int entityId)
    {
        NextActTick = globalTick + StatMath.ActionPeriod(entityId) * cost;
    }
}

// State: fixed-period scheduler for non-statted actors. Plants and inert
// tickers pace themselves with a literal period.
public class FixedPaced : IScheduler
{
    public int period;
    public int NextActTick { get; set; }

    public bool IsDue(int globalTick)
    {
        return globalTick >= NextActTick;
    }

    public void Reschedule(int globalTick, int cost, int entityId)
    {
        NextActTick = globalTick + period * cost;
    }
}

// ----------------------------------------------------------------------------
// Helper — fetch whichever scheduler an entity has attached. Returns null if
// the entity isn't on a schedule at all (terrain, corpses, spawners).
// ----------------------------------------------------------------------------
public static class Scheduling
{
    public static IScheduler? Get(int id)
    {
        if (World.HasComponent<AgilityPaced>(id)) return World.GetComponent<AgilityPaced>(id);
        if (World.HasComponent<FixedPaced>(id))   return World.GetComponent<FixedPaced>(id);
        return null;
    }
}
