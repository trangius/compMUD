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

        string label = World.Label(id);

        // Spawn a corpse entity at the death spot. Its Yields are copied from
        // the dying creature's declaration — meat, pelt, bones for a rabbit,
        // etc. Yields are latent (items appear only when drained by eating or
        // butchering). The corpse stays until its entries are all gone.
        if (World.HasComponent<Yields>(id) && World.HasComponent<Position>(id))
        {
            Position pos = World.GetComponent<Position>(id);
            Yields template = World.GetComponent<Yields>(id);

            int corpseId = World.CreateEntity();
            World.AttachComponent(corpseId, new Position(pos.X, pos.Y));
            World.AttachComponent(corpseId, new Appearance { spriteId = "corpse", layer = 3 });
            World.AttachComponent(corpseId, new Named { name = $"{label} corpse" });
            World.AttachComponent(corpseId, new Walkable());
            World.AttachComponent(corpseId, new Corpse());

            // Fresh Yields instance on the corpse — copy, don't share the list.
            Yield[] copied = new Yield[template.entries.Count];
            for (int i = 0; i < template.entries.Count; i++)
                copied[i] = new Yield(template.entries[i].category, template.entries[i].amount);
            World.AttachComponent(corpseId, new Yields(copied));
        }

        World.Log($"{label} dies");
        World.DestroyEntity(id);
        return true;
    }
}
