namespace Engine;

// Yield, Yields, ResourceCategory, Resources — cross-cutting primitives
// (a tree yields wood, a corpse yields meat) — live in Engine/Yields.cs.

// State: what resource kinds a creature will eat, and at what hunger level it
// starts seeking food. Checked by FeedBehavior.
// hungerThreshold = 0.9 → opportunistic (eats whenever below 90% Max Energy);
// hungerThreshold = 0.3 → stubborn browser (only eats when near starving).
// Plugins can pass their own ResourceCategory instances — extensible without engine changes.
public class Diet
{
    public HashSet<ResourceCategory> allowed;
    public double hungerThreshold = 0.6;  // start seeking food at this fraction of Max Energy

    public Diet(params ResourceCategory[] kinds)
    {
        allowed = new HashSet<ResourceCategory>(kinds);
    }

    // ----------------------------------------------------------------------------
    // Can this creature eat a resource of the given kind?
    // ----------------------------------------------------------------------------
    public bool Accepts(ResourceCategory resourceType)
    {
        return allowed.Contains(resourceType);
    }

    // ----------------------------------------------------------------------------
    // Is the given Energy below this creature's hunger threshold? Used by
    // FeedBehavior to decide whether to seek food this tick.
    // ----------------------------------------------------------------------------
    public bool IsHungry(Energy energy)
    {
        return energy.Current < energy.Max * hungerThreshold;
    }
}

// Behavior: when hungry, drain every edible yield from a Yields source on this
// eater's own tile, or walk (via BFS) toward the nearest reachable Yields source
// that has something we accept. Ties break on cluster density; further ties
// break randomly.
public class FeedBehavior : IBehavior
{
    public int Priority => 30;

    private Random rng;

    // Cached between WouldAct and Act — what we chose and the next step to take.
    private int cachedFoodId = -1;
    private bool cachedFoodIsUnderfoot;
    private int cachedStepDx;
    private int cachedStepDy;

    public FeedBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Hungry AND (an edible Yields source underfoot OR a reachable one we can
    // BFS to)? Underfoot wins instantly. Otherwise flood cells by walking distance,
    // then among reachable edibles prefer the nearest, prefer the densest cluster,
    // and pick randomly from whatever survives both tiebreaks.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id) || !World.HasComponent<Position>(id) || !World.HasComponent<Diet>(id)) return false;

        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        if (!diet.IsHungry(energy)) return false;

        Position pos = World.GetComponent<Position>(id);

        // First: any edible Yields source underfoot we can eat right now?
        foreach (int other in World.EntitiesAt(pos.X, pos.Y))
        {
            if (other == id) continue;
            if (!IsEdible(other, diet)) continue;

            cachedFoodId = other;
            cachedFoodIsUnderfoot = true;
            return true;
        }

        // Otherwise: flood reachable cells out to vision range. CanCreatureBeHere
        // is the same passability the mover uses — walls, water, and other Solids block.
        int range = StatMath.VisionRange(id);
        BFSResult bfs = Algorithms.BFS(pos.X, pos.Y, range, World.CanCreatureBeHere, rng);

        // Collect every edible entity sitting in a reached cell, with its walking distance.
        List<(int foodId, int dist)> reachable = new List<(int, int)>();
        foreach (KeyValuePair<(int x, int y), int> entry in bfs.distance)
        {
            foreach (int other in World.EntitiesAt(entry.Key.x, entry.Key.y))
            {
                if (other == id) continue;
                if (!IsEdible(other, diet)) continue;
                reachable.Add((other, entry.Value));
            }
        }

        if (reachable.Count == 0) return false;

        // Keep only edibles tied for the shortest walking distance.
        int bestDist = int.MaxValue;
        foreach ((int foodId, int dist) in reachable)
            if (dist < bestDist) bestDist = dist;
        List<int> nearest = new List<int>();
        foreach ((int foodId, int dist) in reachable)
            if (dist == bestDist) nearest.Add(foodId);

        // First tiebreak: most edible 8-neighbors (prefer food in a dense patch).
        int bestNeighborCount = -1;
        List<int> topCandidates = new List<int>();
        foreach (int candidate in nearest)
        {
            int n = CountFoodNeighbors(candidate, diet);
            if (n > bestNeighborCount)
            {
                bestNeighborCount = n;
                topCandidates.Clear();
            }
            if (n == bestNeighborCount)
                topCandidates.Add(candidate);
        }

        // Final tiebreak: random pick among survivors.
        cachedFoodId = topCandidates[rng.Next(topCandidates.Count)];
        cachedFoodIsUnderfoot = false;

        // Cache the first step along the BFS path — Act just calls TryMove with it.
        Position foodPos = World.GetComponent<Position>(cachedFoodId);
        (cachedStepDx, cachedStepDy) = bfs.FirstStep(foodPos.X, foodPos.Y);
        return true;
    }

    // ----------------------------------------------------------------------------
    // Drain every edible yield from the underfoot source in one action (cost 5 —
    // eating takes real time). If the source has no yields left afterward, destroy
    // it. If it still has durable yields (pelt, bones), leave it but flip a corpse's
    // sprite to bones so the visual matches what's left. Walking case: step along
    // the cached BFS path (cost 1).
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (cachedFoodIsUnderfoot)
        {
            Diet diet = World.GetComponent<Diet>(id);
            Energy energy = World.GetComponent<Energy>(id);
            Yields yields = World.GetComponent<Yields>(cachedFoodId);

            // Snapshot the edible categories — we can't iterate yields.entries while
            // Drain mutates it, so collect the targets first.
            List<ResourceCategory> targets = new List<ResourceCategory>();
            foreach (Yield y in yields.entries)
                if (diet.Accepts(y.category)) targets.Add(y.category);

            int total = 0;
            foreach (ResourceCategory cat in targets)
                total += yields.Drain(cat, int.MaxValue);
            energy.Restore(total);

            World.Log($"{World.Label(id)} eats {World.Label(cachedFoodId)}");

            // Post-drain: source vanishes if empty, or turns to bones if it was a
            // corpse and its meat is gone. Bushes simply empty out and despawn.
            if (yields.entries.Count == 0)
            {
                World.DestroyEntity(cachedFoodId);
            }
            else if (World.HasComponent<Corpse>(cachedFoodId) && yields.Get(Resources.Meat) == null)
            {
                // Stripped corpse — picked-clean look. Rename so labels match.
                if (World.HasComponent<Appearance>(cachedFoodId))
                    World.GetComponent<Appearance>(cachedFoodId).spriteId = "bones";
                if (World.HasComponent<Named>(cachedFoodId))
                {
                    Named n = World.GetComponent<Named>(cachedFoodId);
                    n.name = n.name.Replace("corpse", "bones");
                }
            }
            return 5;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }

    // ----------------------------------------------------------------------------
    // A Yields source with at least one yield this eater accepts. Works for any
    // source — a grazable bush, a corpse on the ground, a tree (later).
    // ----------------------------------------------------------------------------
    private static bool IsEdible(int id, Diet diet)
    {
        if (!World.HasComponent<Yields>(id)) return false;
        // Live creatures have Yields too (they'll drop a corpse when killed), but
        // they're not food until dead — Hunt handles that. Gate on Health.
        if (World.HasComponent<Health>(id)) return false;
        // Any yield on this source the eater's diet accepts?
        foreach (Yield y in World.GetComponent<Yields>(id).entries)
            if (diet.Accepts(y.category)) return true;
        return false;
    }

    // ----------------------------------------------------------------------------
    // Count of the target's 8 neighbor cells that contain at least one edible.
    // One increment per cell — a stacked pair of sources doesn't inflate the score.
    // ----------------------------------------------------------------------------
    private static int CountFoodNeighbors(int targetId, Diet diet)
    {
        if (!World.HasComponent<Position>(targetId)) return 0;
        Position p = World.GetComponent<Position>(targetId);

        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                foreach (int other in World.EntitiesAt(p.X + dx, p.Y + dy))
                {
                    if (other == targetId) continue;
                    if (IsEdible(other, diet))
                    {
                        count++;
                        break;  // one per cell is enough
                    }
                }
            }
        }
        return count;
    }
}
