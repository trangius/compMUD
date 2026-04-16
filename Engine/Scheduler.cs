namespace Engine;

// State: paces an entity's Behaviors dispatch. period is the base number of
// global ticks between this entity's actions; nextActTick is the global clock
// value at which it is next due to act. A wolf with period=1 acts every tick;
// a bush with period=5 acts every fifth tick. Effects still fire every global
// tick on every entity regardless of pace — Scheduler only gates behaviors.
public class Scheduler
{
    public int period = 1;       // ticks between actions; must be >= 1
    public int nextActTick = 0;  // global tick at which this entity next acts

    // ----------------------------------------------------------------------------
    // Is this entity due to act at the given global tick?
    // ----------------------------------------------------------------------------
    public bool IsDue(int globalTick)
    {
        return globalTick >= nextActTick;
    }

    // ----------------------------------------------------------------------------
    // Push nextActTick forward by the entity's period. Called by the tick
    // dispatcher right after the entity's behaviors are run.
    // ----------------------------------------------------------------------------
    public void Reschedule(int globalTick)
    {
        nextActTick = globalTick + period;
    }
}
