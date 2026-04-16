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

// State: what resource kinds a creature will eat. Checked by FeedBehavior and HarvestBehavior.
// Plugins can pass their own ResourceCategory instances — extensible without engine changes.
public class Diet
{
    public HashSet<ResourceCategory> allowed;

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

        Energy energy = World.GetComponent<Energy>(id);
        if (energy.Current >= energy.Max * 0.6) return false;

        Diet diet = World.GetComponent<Diet>(id);
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
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);
        Drops drops = World.GetComponent<Drops>(cachedHarvestableId);
        drops.SpawnItem(pos.X, pos.Y);
        World.Log($"{World.GetEntityName(id)} harvests {World.GetEntityName(cachedHarvestableId, drops.name)}");
        World.DestroyEntity(cachedHarvestableId);
    }
}

// Behavior: when hungry, eat a ResourceItem underfoot we can eat,
// or walk toward the nearest edible food source.
public class FeedBehavior : IBehavior
{
    public int Priority => 30;

    // Cached between WouldAct and Act — which food to eat or approach.
    private int cachedFoodId = -1;
    private bool cachedFoodIsUnderfoot;

    // ----------------------------------------------------------------------------
    // Hungry AND (edible food underfoot OR visible edible food)? Cache what and where.
    // Prefers food underfoot — eating beats moving.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id) || !World.HasComponent<Position>(id) || !World.HasComponent<Diet>(id)) return false;

        Energy energy = World.GetComponent<Energy>(id);
        if (energy.Current >= energy.Max * 0.6) return false;

        Diet diet = World.GetComponent<Diet>(id);
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

        // Otherwise: look for the nearest edible food source we could walk toward.
        // Edible = a ResourceItem (loose food) or a harvestable with matching resourceType.
        if (!World.HasComponent<Sensing>(id)) return false;
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedFoodId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            (World.HasComponent<ResourceItem>(other) && diet.Accepts(World.GetComponent<ResourceItem>(other).resourceType))
            || (World.HasComponent<Drops>(other) && !World.HasComponent<Health>(other) && diet.Accepts(World.GetComponent<Drops>(other).resourceType)));

        if (cachedFoodId < 0) return false;

        cachedFoodIsUnderfoot = false;
        return true;
    }

    // ----------------------------------------------------------------------------
    // Eat the cached food if it's underfoot; otherwise walk toward it.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);

        if (cachedFoodIsUnderfoot)
        {
            // Consume the item, restore energy, remove it
            Energy energy = World.GetComponent<Energy>(id);
            ResourceItem item = World.GetComponent<ResourceItem>(cachedFoodId);
            energy.Restore(item.amount);
            World.Log($"{World.GetEntityName(id)} eats {World.GetEntityName(cachedFoodId)}");
            World.DestroyEntity(cachedFoodId);
            return;
        }

        Position foodPos = World.GetComponent<Position>(cachedFoodId);
        MovementHelper.MoveToward(id, pos, foodPos.X, foodPos.Y);
    }
}
