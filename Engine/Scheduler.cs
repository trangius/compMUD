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
//
// TODO: AgilityPaced and FixedPaced share ~4 lines of logic (IsDue, the
// tickCount + period shape in OnAttach/Reschedule) — the only real difference
// is where "period" comes from. Kept separate on purpose: the two class names
// make the statted-vs-simpleton split visible at the archetype. Revisit if a
// third scheduler variant shows up and the duplication starts to bite.
// ----------------------------------------------------------------------------

public interface IScheduler
{
    int NextActTick { get; set; }
    bool IsDue(int globalTick);
    void Reschedule(int globalTick, int cost, int entityId);
}

// State: stat-driven scheduler. Period = StatMath.ActionPeriod(id).
// Requires Stats on the entity — dispatcher + StatMath will throw if missing.
public class AgilityPaced : IScheduler, IOnAttach
{
    public int NextActTick { get; set; }

    // ----------------------------------------------------------------------------
    // On attach, set the first action one full period out from "now". Without
    // this a late-spawned creature has NextActTick=0, which is already in the
    // past — it'd act on its very next tick regardless of pace. Requires Stats
    // to already be on the entity (archetype order handles that).
    // ----------------------------------------------------------------------------
    public void OnAttach(int id)
    {
        NextActTick = World.tickCount + StatMath.ActionPeriod(id);
    }

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
public class FixedPaced : IScheduler, IOnAttach
{
    public int period;
    public int NextActTick { get; set; }

    // ----------------------------------------------------------------------------
    // On attach, wait one full period before the first action. Same reason as
    // AgilityPaced — don't let mid-game spawns get a free action on tick 0.
    // ----------------------------------------------------------------------------
    public void OnAttach(int id)
    {
        NextActTick = World.tickCount + period;
    }

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
