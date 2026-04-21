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
    // Hungry AND (an edible Yields source underfoot OR a reachable one the
    // shared flow field can lead us to)? Underfoot wins instantly. Otherwise
    // FlowFieldHelper picks the best-step neighbor across every diet-category
    // flow field. The previous cluster-density tiebreak is gone — the flow
    // field gives a direction, not a target entity, so there's nothing to
    // compare density against. Random pick across equally-close directions
    // spreads a herd out just as well in practice.
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

        // Union the flow fields for every diet category. A corpse carries
        // multiple yields, so the same bush / corpse can appear in several
        // fields — picking the min distance across fields is the right answer.
        List<FlowField> fields = new List<FlowField>();
        foreach (ResourceCategory cat in diet.allowed)
            fields.Add(World.GetYieldFlowField(cat));

        int range = StatMath.VisionRange(id);

        if (!FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, range, rng, out FlowFieldStep step))
            return false;

        // cachedFoodId isn't used on the walking path — we don't pre-pick a
        // specific source. Next tick's Feed fires again from the new cell,
        // re-reads the (possibly newly-computed) field, and either finds food
        // underfoot (eat) or takes another step.
        cachedStepDx = step.stepDx;
        cachedStepDy = step.stepDy;
        cachedFoodIsUnderfoot = false;
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

}
