namespace Engine;

// ----------------------------------------------------------------------------
// State: energy drains each tick. When it runs out, the entity starves and takes damage.
// ----------------------------------------------------------------------------

public class Energy
{
    public int Current { get; private set; }
    public int Max { get; }

    // Energy units drained per tick. Fractional (e.g. 0.25) for slower
    // metabolisms; the accumulator below carries the carry-over so Current
    // only drops by whole units.
    private double drainPerTick;
    private double drainAccumulator;

    public Energy(int max, double drainPerTick = 1.0)
    {
        Max = max;
        Current = max;
        this.drainPerTick = drainPerTick;
    }

    // ----------------------------------------------------------------------------
    // Tick forward by drainPerTick. Drop whole units once the accumulator
    // crosses 1 — no partial-unit state, Current stays clean integer.
    // ----------------------------------------------------------------------------
    public void Drain()
    {
        drainAccumulator += drainPerTick;
        while (drainAccumulator >= 1.0)
        {
            Current = Math.Max(0, Current - 1);
            drainAccumulator -= 1.0;
        }
    }
    public void Restore(int amount) { Current = Math.Min(Max, Current + amount); }

    // Force-set the current value. Used by area builders that want their starting
    // creatures hungry so Feed behaviors fire from tick one instead of after a
    // long Rest-then-drain wait. Clamped to [0, Max].
    public void SetCurrent(int value) { Current = Math.Clamp(value, 0, Max); }
}

// Effect: drain one step of energy per tick, starve for 1 HP when empty.
public class EnergyDrainEffect : IEffect
{
    // ----------------------------------------------------------------------------
    // Tick the host's Energy down; if it hits zero, bleed 1 HP from Health and
    // reap the corpse if that kills them.
    // ----------------------------------------------------------------------------
    public void Apply(int id)
    {
        if (!World.HasComponent<Energy>(id)) return;

        Energy energy = World.GetComponent<Energy>(id);
        energy.Drain();

        if (energy.Current <= 0 && World.HasComponent<Health>(id))
        {
            World.GetComponent<Health>(id).TakeDamage(1);
            DeathHelper.DestroyEntityIfDead(id);
        }
    }
}
