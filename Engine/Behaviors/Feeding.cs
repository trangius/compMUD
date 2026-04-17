namespace Engine;

// ----------------------------------------------------------------------------
// Category: a kind of resource (meat, plant, plastic, ...). Real object, not a
// type tag — each built-in kind is a singleton in the Resources registry below.
// Identity is the object reference (pointer equality); the name is for display only.
// Plugins extend by declaring their own static readonly ResourceCategory somewhere
// and using it at call sites — no engine change needed.
// ----------------------------------------------------------------------------
public class ResourceCategory
{
    public readonly string name;  // singleton label; readonly so Resources.Meat.name can never drift

    public ResourceCategory(string name)
    {
        this.name = name;
    }
}

// Built-in resource kinds. Each static readonly field is a singleton instance.
public static class Resources
{
    public static readonly ResourceCategory Meat = new("meat");
    public static readonly ResourceCategory Berry = new("berry");
}

// State: what this entity yields when destroyed (killed, harvested, grazed).
public class Drops
{
    public required string name;
    public required ResourceCategory resourceType;  // must match an eater's Diet
    public required int amount;                  // no silent zero-energy drops
    public required string dropSpriteId;
    public int dropLayer = 3;

    // ----------------------------------------------------------------------------
    // Create a resource item in the world from these drop values.
    // ----------------------------------------------------------------------------
    public int SpawnItem(int x, int y)
    {
        int item = World.CreateEntity();
        World.AttachComponent(item, new Position(x, y));
        World.AttachComponent(item, new Appearance { spriteId = dropSpriteId, layer = dropLayer });
        World.AttachComponent(item, new Named { name = name });
        World.AttachComponent(item, new ResourceItem { resourceType = resourceType, amount = amount });
        World.AttachComponent(item, new Walkable());
        return item;
    }
}

// State: a resource sitting in the world. Can be consumed or picked up.
public class ResourceItem
{
    public required ResourceCategory resourceType;
    public required int amount;
}

// State: what resource kinds a creature will eat, and at what hunger level it
// starts seeking food. Checked by FeedBehavior and HarvestBehavior.
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
    // HarvestBehavior and FeedBehavior to decide whether to seek food this tick.
    // ----------------------------------------------------------------------------
    public bool IsHungry(Energy energy)
    {
        return energy.Current < energy.Max * hungerThreshold;
    }
}

// Behavior: when hungry and standing on something harvestable we can eat,
// destroy it and drop the resource.
public class HarvestBehavior : IBehavior
{
    public int Priority => 40;

    // Cached between WouldAct and Act — which harvestable to destroy.
    private int cachedHarvestableId = -1;

    // ----------------------------------------------------------------------------
    // Hungry AND standing on a harvestable (Drops, no Health) we'd eat? Cache it.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id) || !World.HasComponent<Position>(id) || !World.HasComponent<Diet>(id)) return false;

        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        if (!diet.IsHungry(energy)) return false;

        Position pos = World.GetComponent<Position>(id);

        // Harvestable = has Drops but no Health (a bush, not a live creature) AND we eat its kind
        foreach (int other in World.EntitiesAt(pos.X, pos.Y))
        {
            if (other == id) continue;
            if (!World.HasComponent<Drops>(other)) continue;
            if (World.HasComponent<Health>(other)) continue;
            if (!diet.Accepts(World.GetComponent<Drops>(other).resourceType)) continue;

            cachedHarvestableId = other;
            return true;
        }

        return false;
    }

    // ----------------------------------------------------------------------------
    // Spawn the cached harvestable's drop at our feet, then destroy it.
    // Cost 1 — grabbing a berry at your feet is a quick action.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);
        Drops drops = World.GetComponent<Drops>(cachedHarvestableId);
        drops.SpawnItem(pos.X, pos.Y);
        World.Log($"{World.GetEntityName(id)} harvests {World.GetEntityName(cachedHarvestableId, drops.name)}");
        World.DestroyEntity(cachedHarvestableId);
        return 1;
    }
}

// Behavior: when hungry, eat a ResourceItem underfoot, or walk (via BFS) toward
// the nearest edible food we can actually reach. Ties break on cluster density;
// further ties break randomly.
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
    // Hungry AND (edible food underfoot OR a reachable edible we can BFS to)?
    // Underfoot wins instantly. Otherwise flood cells by walking distance, then
    // among reachable edibles prefer the nearest, prefer the densest cluster,
    // and pick randomly from whatever survives both tiebreaks.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id) || !World.HasComponent<Position>(id) || !World.HasComponent<Diet>(id)) return false;

        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        if (!diet.IsHungry(energy)) return false;

        Position pos = World.GetComponent<Position>(id);

        // First: any edible ResourceItem underfoot we can eat right now?
        foreach (int other in World.EntitiesAt(pos.X, pos.Y))
        {
            if (other == id) continue;
            if (!World.HasComponent<ResourceItem>(other)) continue;
            if (!diet.Accepts(World.GetComponent<ResourceItem>(other).resourceType)) continue;

            cachedFoodId = other;
            cachedFoodIsUnderfoot = true;
            return true;
        }

        // Otherwise: flood reachable cells out to vision range. IsCreatureSpawnable
        // is the same passability the mover uses — walls, water, and other Solids block.
        if (!World.HasComponent<Sensing>(id)) return false;
        int range = World.GetComponent<Sensing>(id).VisionRange;
        BFSResult bfs = Algorithms.BFS(pos.X, pos.Y, range, World.IsCreatureSpawnable);

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
    // Eat the cached food if it's underfoot (cost 5 — chewing takes real time);
    // otherwise step along the cached path (cost 1 — baseline movement).
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (cachedFoodIsUnderfoot)
        {
            // Consume the item, restore energy, remove it
            Energy energy = World.GetComponent<Energy>(id);
            ResourceItem item = World.GetComponent<ResourceItem>(cachedFoodId);
            energy.Restore(item.amount);
            World.Log($"{World.GetEntityName(id)} eats {World.GetEntityName(cachedFoodId)}");
            World.DestroyEntity(cachedFoodId);
            return 5;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }

    // ----------------------------------------------------------------------------
    // A loose ResourceItem we'll eat, or a harvestable (Drops without Health) we'll eat?
    // ----------------------------------------------------------------------------
    private static bool IsEdible(int id, Diet diet)
    {
        if (World.HasComponent<ResourceItem>(id))
            return diet.Accepts(World.GetComponent<ResourceItem>(id).resourceType);
        if (World.HasComponent<Drops>(id) && !World.HasComponent<Health>(id))
            return diet.Accepts(World.GetComponent<Drops>(id).resourceType);
        return false;
    }

    // ----------------------------------------------------------------------------
    // Count of the target's 8 neighbor cells that contain at least one edible.
    // One increment per cell — a stacked pair of items doesn't inflate the score.
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
