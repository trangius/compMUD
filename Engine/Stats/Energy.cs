namespace Engine;

// ----------------------------------------------------------------------------
// State: energy drains each tick. When it runs out, the entity starves and takes damage.
// ----------------------------------------------------------------------------

public class Energy
{
    public int Current { get; private set; }
    public int Max { get; }

    private int drainRate;

    public Energy(int max, int drainRate = 1)
    {
        Max = max;
        Current = max;
        this.drainRate = drainRate;
    }

    public void Drain() { Current = Math.Max(0, Current - drainRate); }
    public void Restore(int amount) { Current = Math.Min(Max, Current + amount); }
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
