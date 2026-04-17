namespace Engine;

// Behavior: step in a random direction. Lowest priority — the fallback.
public class WanderBehavior : IBehavior
{
    public int Priority => 0;

    private Random rng;

    public WanderBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Always willing — Wander is the fallback that runs when nothing else does.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        return World.HasComponent<Position>(id);
    }

    // ----------------------------------------------------------------------------
    // Pick a random direction and try to step. Cost 1 — a step is baseline.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        MovementHelper.Wander(id, rng);
        return 1;
    }
}
