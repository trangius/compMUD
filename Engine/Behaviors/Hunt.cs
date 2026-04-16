namespace Engine;

// State: this entity hunts specific species. `hunts` is a set of archetype
// spawn delegates — the same pointers used as Species identity. A wolf with
// hunts = { CreateRabbit } will chase rabbits (and only rabbits). Set membership
// replaces the old role marker, the same way Diet.Accepts checks resource kinds.
public class Predator
{
    public HashSet<Func<int, int, int>> hunts;

    public Predator(params Func<int, int, int>[] prey)
    {
        hunts = new HashSet<Func<int, int, int>>(prey);
    }

    // ----------------------------------------------------------------------------
    // Does my prey list include this species?
    // ----------------------------------------------------------------------------
    public bool Hunts(Func<int, int, int> speciesSpawn)
    {
        return hunts.Contains(speciesSpawn);
    }
}

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
    // Find the nearest visible prey whose species is on this hunter's list.
    // Cache it for Act.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Attacking>(id) || !World.HasComponent<Predator>(id) || !World.HasComponent<Position>(id)) return false;

        Predator predator = World.GetComponent<Predator>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedPreyId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id
            && World.HasComponent<Species>(other)
            && predator.Hunts(World.GetComponent<Species>(other).spawn)
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
