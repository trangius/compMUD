namespace Engine;

// State: this entity is a predator. Used by FleeBehavior to detect threats.
public class Hunts { }

// State: attack damage this entity deals in melee.
public class Attacking
{
    public int Damage { get; }

    public Attacking(int damage)
    {
        Damage = damage;
    }
}

// Behavior: chase the nearest prey. Bite if adjacent, chase if distant.
public class HuntBehavior : IBehavior
{
    public int Priority => 50;

    // Cached between WouldAct and Act — which prey to go for.
    private int cachedPreyId = -1;

    // ----------------------------------------------------------------------------
    // Find the nearest visible prey. Cache it for Act.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Attacking>(id) || !World.HasComponent<Position>(id)) return false;

        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedPreyId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id
            && World.HasComponent<Flees>(other)
            && World.HasComponent<Health>(other));

        return cachedPreyId >= 0;
    }

    // ----------------------------------------------------------------------------
    // If cached prey is adjacent, bite it. Otherwise step toward it.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);
        Position preyPos = World.GetComponent<Position>(cachedPreyId);
        int dist = Math.Abs(preyPos.X - pos.X) + Math.Abs(preyPos.Y - pos.Y);

        if (dist <= 1)
        {
            // Bite: deal damage, log it, remove prey if dead
            Attacking attack = World.GetComponent<Attacking>(id);
            Health targetHealth = World.GetComponent<Health>(cachedPreyId);
            targetHealth.TakeDamage(attack.Damage);

            World.Log($"{World.GetEntityName(id)} attacks {World.GetEntityName(cachedPreyId)} ({targetHealth.Current}/{targetHealth.Max} HP)");
            DeathHelper.DestroyEntityIfDead(cachedPreyId);
        }
        else
        {
            MovementHelper.MoveToward(id, pos, preyPos.X, preyPos.Y);
        }
    }
}
