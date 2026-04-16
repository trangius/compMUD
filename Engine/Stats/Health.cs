namespace Engine;

// ----------------------------------------------------------------------------
// State: hit points. When they reach zero, the entity dies.
// ----------------------------------------------------------------------------

public class Health
{
    public int Current { get; private set; }
    public int Max { get; }
    public bool IsDead => Current <= 0;

    public Health(int max)
    {
        Max = max;
        Current = max;
    }

    public void TakeDamage(int amount) { Current = Math.Max(0, Current - amount); }
}

// State: this entity is a corpse. Added by DeathHelper when a creature dies.
// Lives here next to where it's born (DeathHelper below).
public class Corpse { }

// ----------------------------------------------------------------------------
// Check if something just died — spawn its drops and remove it from the world.
// ----------------------------------------------------------------------------
public static class DeathHelper
{
    public static bool DestroyEntityIfDead(int id)
    {
        if (!World.HasComponent<Health>(id)) return false;
        if (!World.GetComponent<Health>(id).IsDead) return false;

        string name = World.GetEntityName(id, "unknown");

        // Drop resources where the entity died, and mark the drop as a Corpse
        // (distinguishes creature remains from bush berries for counters/queries)
        if (World.HasComponent<Drops>(id) && World.HasComponent<Position>(id))
        {
            Position pos = World.GetComponent<Position>(id);
            int corpseId = World.GetComponent<Drops>(id).SpawnItem(pos.X, pos.Y);
            World.AttachComponent(corpseId, new Corpse());
        }

        World.Log($"{name} dies");
        World.DestroyEntity(id);
        return true;
    }
}
