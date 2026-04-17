namespace Engine;

// Behavior: do nothing. Runs when the entity is well fed and no higher-priority
// behavior claims the tick — fed rabbits don't wander off their full bellies.
// Beats Wander (priority 0); loses to Flee / Hunt / Harvest / Feed / Breed.
public class RestBehavior : IBehavior
{
    public int Priority => 1;

    // Same threshold FeedBehavior uses to stop seeking food — keeps the two in step.
    private const double minEnergyFraction = 0.6;

    // ----------------------------------------------------------------------------
    // Willing to rest iff Energy is above the "fed" threshold. Below it, Feed runs.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id)) return false;
        Energy energy = World.GetComponent<Energy>(id);
        return energy.Current > energy.Max * minEnergyFraction;
    }

    // ----------------------------------------------------------------------------
    // Rest is a no-op by design — the entity keeps its position and its
    // components. Cost 1 — one period of peaceful sitting around.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        // Intentionally empty — resting is the absence of motion.
        return 1;
    }
}
