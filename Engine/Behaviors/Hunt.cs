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

// Behavior: chase the nearest REACHABLE prey. Same BFS-first pattern as Feed —
// flood the walkable cells once, then scan the flood for prey and pick the one
// whose nearest approach cell has the smallest BFS distance. No Euclidean
// vision circle: "visible" and "reachable" are the same concept here, so wolves
// never lock onto prey that's technically in sight but walled off.
//
// Prey's own cell is Solid so BFS can't enter it — we target the nearest of
// the prey's 8 neighbors and walk there. If one of those neighbors is the
// wolf's own cell (distance 0), the wolf is already adjacent and bites.
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

    // 8-connected adjacency — a diagonal cell next to the prey is a valid bite spot.
    private static readonly (int dx, int dy)[] adjacentOffsets = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

    // ----------------------------------------------------------------------------
    // BFS the reachable cells once, then pick the nearest reachable prey on this
    // hunter's species list. "Nearest reachable" = the prey whose closest 8-neighbor
    // has the smallest BFS distance. Distance 0 means the hunter is already next
    // to its prey (wolf's own cell IS a neighbor of prey), so we cache a bite.
    // No reachable prey? Decline — Hunt yields the tick to a lower-priority behavior.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Attacking>(id) || !World.HasComponent<Predator>(id) || !World.HasComponent<Position>(id)) return false;

        Predator predator = World.GetComponent<Predator>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        // Single BFS flood. Same passability as the mover uses, so anything we
        // find a path to is genuinely reachable this tick.
        BFSResult bfs = Algorithms.BFS(pos.X, pos.Y, range, World.IsCreatureSpawnable);

        // Scan every species-holder in the world; keep the reachable ones on our
        // prey list. For each, find its closest BFS-reachable 8-neighbor.
        int bestPrey = -1;
        int bestDist = int.MaxValue;
        (int x, int y) bestApproach = (-1, -1);
        foreach (int other in World.AllWithComponent<Species>())
        {
            if (other == id) continue;
            if (!predator.Hunts(World.GetComponent<Species>(other).spawn)) continue;
            if (!World.HasComponent<Health>(other)) continue;

            Position preyPos = World.GetComponent<Position>(other);

            // Nearest approach cell for this particular prey
            int preyBestDist = int.MaxValue;
            (int x, int y) preyBestCell = (-1, -1);
            foreach ((int dx, int dy) offset in adjacentOffsets)
            {
                int cx = preyPos.X + offset.dx;
                int cy = preyPos.Y + offset.dy;
                if (!bfs.Reachable(cx, cy)) continue;
                int d = bfs.Distance(cx, cy);
                if (d < preyBestDist)
                {
                    preyBestDist = d;
                    preyBestCell = (cx, cy);
                }
            }

            if (preyBestDist < bestDist)
            {
                bestDist = preyBestDist;
                bestPrey = other;
                bestApproach = preyBestCell;
            }
        }

        if (bestPrey < 0) return false;  // no reachable prey on our hunt list

        cachedPreyId = bestPrey;
        // bestDist == 0 means a prey-adjacent cell is OUR cell — we're adjacent, bite.
        if (bestDist == 0)
        {
            cachedPreyAdjacent = true;
        }
        else
        {
            cachedPreyAdjacent = false;
            (cachedStepDx, cachedStepDy) = bfs.FirstStep(bestApproach.x, bestApproach.y);
        }
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
