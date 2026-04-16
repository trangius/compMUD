namespace Engine;

// State: this wolf is on a one-shot raid — come in from the forest, kill one
// rabbit, return to any tree cell, then despawn. Kept separate from generic
// wolves so a future pack wolf can persist with the same Hunt logic.
public class RaidingWolf
{
    public bool hasKilled = false;
}

// Behavior: after the kill, walk back to any tree cell via BFS. Once standing
// on a tree cell, the wolf destroys itself — "vanishes into the forest".
// Priority 50 beats Harvest/Feed/Hunt — once the mission is done, nothing else
// matters; get out.
public class ReturnToForestBehavior : IBehavior
{
    public int Priority => 50;

    // Cached between WouldAct and Act
    private int cachedStepDx;
    private int cachedStepDy;
    private bool cachedOnTree;

    // ----------------------------------------------------------------------------
    // Only active for a raider that has killed. If already on a tree cell, cache
    // "despawn on Act". Otherwise BFS to the nearest reachable tree and cache
    // the first step along the shortest path. Unreachable trees → decline tick.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<RaidingWolf>(id)) return false;
        if (!World.GetComponent<RaidingWolf>(id).hasKilled) return false;
        if (!World.HasComponent<Position>(id)) return false;

        Position pos = World.GetComponent<Position>(id);

        // Standing on a tree already? Vanish on Act.
        foreach (int other in World.EntitiesAt(pos.X, pos.Y))
        {
            if (other == id) continue;
            if (World.HasComponent<Tree>(other))
            {
                cachedOnTree = true;
                return true;
            }
        }

        // Flood reachable cells and pick the nearest tree
        int range = World.HasComponent<Sensing>(id) ? World.GetComponent<Sensing>(id).VisionRange : 100;
        BFSResult bfs = Algorithms.BFS(pos.X, pos.Y, range, World.IsCreatureSpawnable);

        int bestDist = int.MaxValue;
        (int x, int y) bestCell = (-1, -1);
        foreach (int treeId in World.AllWithComponent<Tree>())
        {
            if (!World.HasComponent<Position>(treeId)) continue;
            Position treePos = World.GetComponent<Position>(treeId);
            if (!bfs.Reachable(treePos.X, treePos.Y)) continue;
            int d = bfs.Distance(treePos.X, treePos.Y);
            if (d < bestDist)
            {
                bestDist = d;
                bestCell = (treePos.X, treePos.Y);
            }
        }

        if (bestDist == int.MaxValue) return false;

        cachedOnTree = false;
        (cachedStepDx, cachedStepDy) = bfs.FirstStep(bestCell.x, bestCell.y);
        return true;
    }

    // ----------------------------------------------------------------------------
    // Tree underfoot → despawn. Otherwise step along the cached path.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        if (cachedOnTree)
        {
            World.Log($"{World.GetEntityName(id)} vanishes into the forest");
            World.DestroyEntity(id);
            return;
        }
        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
    }
}

// Effect: per-tick Poisson-style trigger for wolf raids. Each global tick, rolls
// a small chance of spawning a wolf at a random tree cell. Attached to a
// singleton spawner entity (see Archetypes.CreateWolfRaidSpawner).
public class WolfRaidEffect : IEffect
{
    public double raidChance = 0.0015;  // probability per tick — roughly 1 raid per 670 ticks

    private Random rng;

    public WolfRaidEffect(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Roll the raid chance. On success, pick a random tree cell that's open and
    // spawn a wolf there. Try a handful of trees in case the first is occupied.
    // ----------------------------------------------------------------------------
    public void Apply(int id)
    {
        if (rng.NextDouble() >= raidChance) return;

        List<int> trees = World.AllWithComponent<Tree>();
        if (trees.Count == 0) return;

        // Try up to 5 random trees before giving up — the forest is usually big enough
        for (int tries = 0; tries < 5; tries++)
        {
            int treeId = trees[rng.Next(trees.Count)];
            if (!World.HasComponent<Position>(treeId)) continue;
            Position treePos = World.GetComponent<Position>(treeId);
            if (!World.IsCreatureSpawnable(treePos.X, treePos.Y)) continue;

            Archetypes.CreateWolf(treePos.X, treePos.Y);
            World.Log($"A wolf emerges from the forest at ({treePos.X},{treePos.Y})");
            return;
        }
    }
}
