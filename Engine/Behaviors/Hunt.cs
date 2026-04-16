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

// Behavior: chase the nearest prey. Bite if adjacent, otherwise BFS a real
// path and step along it. Prey's own cell is Solid so BFS can't enter it —
// instead we target the nearest of the prey's four neighbors and walk there.
//
// Priority sits BELOW Feed on purpose: a hungry wolf with a corpse underfoot
// should eat it before chasing a live rabbit across the map. Feed returns
// false when not hungry or when no food is reachable, so Hunt still runs in
// the normal case — it only yields when there's food already within reach.
public class HuntBehavior : IBehavior
{
    public int Priority => 20;

    // Cached between WouldAct and Act.
    private int cachedPreyId = -1;
    private bool cachedPreyAdjacent;
    private int cachedStepDx;
    private int cachedStepDy;

    private static readonly (int dx, int dy)[] adjacentOffsets = { (0, -1), (0, 1), (1, 0), (-1, 0) };

    // ----------------------------------------------------------------------------
    // Find the nearest visible prey on this hunter's species list. If already
    // adjacent, cache the bite. Otherwise BFS and cache the first step along
    // the shortest path to the nearest cell next to the prey. If no path exists
    // (prey walled off behind impassable terrain), decline — hunting is skipped
    // this tick and the wolf falls through to lower-priority behaviors.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Attacking>(id) || !World.HasComponent<Predator>(id) || !World.HasComponent<Position>(id)) return false;

        Predator predator = World.GetComponent<Predator>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        // Species-match: nearest creature with Species on our hunt list and a Health component
        int preyId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id
            && World.HasComponent<Species>(other)
            && predator.Hunts(World.GetComponent<Species>(other).spawn)
            && World.HasComponent<Health>(other));

        if (preyId < 0) return false;

        Position preyPos = World.GetComponent<Position>(preyId);

        // Already adjacent? Skip the pathfinder and bite next tick.
        if (Math.Abs(preyPos.X - pos.X) + Math.Abs(preyPos.Y - pos.Y) <= 1)
        {
            cachedPreyId = preyId;
            cachedPreyAdjacent = true;
            return true;
        }

        // Flood reachable cells and pick the prey's nearest open neighbor.
        BFSResult bfs = Algorithms.BFS(pos.X, pos.Y, range, World.IsCreatureSpawnable);
        int bestDist = int.MaxValue;
        (int x, int y) bestCell = (-1, -1);
        foreach ((int dx, int dy) offset in adjacentOffsets)
        {
            int cx = preyPos.X + offset.dx;
            int cy = preyPos.Y + offset.dy;
            if (!bfs.Reachable(cx, cy)) continue;
            int d = bfs.Distance(cx, cy);
            if (d < bestDist)
            {
                bestDist = d;
                bestCell = (cx, cy);
            }
        }

        if (bestDist == int.MaxValue) return false;  // prey unreachable by any path

        cachedPreyId = preyId;
        cachedPreyAdjacent = false;
        (cachedStepDx, cachedStepDy) = bfs.FirstStep(bestCell.x, bestCell.y);
        return true;
    }

    // ----------------------------------------------------------------------------
    // Adjacent case: bite. Walking case: step along the cached BFS path.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        if (cachedPreyAdjacent)
        {
            // Bite: deal damage, log it, remove prey if dead
            Attacking attack = World.GetComponent<Attacking>(id);
            Health targetHealth = World.GetComponent<Health>(cachedPreyId);
            targetHealth.TakeDamage(attack.Damage);

            World.Log($"{World.GetEntityName(id)} attacks {World.GetEntityName(cachedPreyId)} ({targetHealth.Current}/{targetHealth.Max} HP)");
            DeathHelper.DestroyEntityIfDead(cachedPreyId);
            return;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
    }
}
