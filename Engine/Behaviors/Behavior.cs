namespace Engine;

// ----------------------------------------------------------------------------
// Every entity with behaviors picks ONE action per tick. Each behavior answers
// WouldAct (caching what it decided), the dispatcher picks the highest-priority
// willing behavior, and only that winner runs Act.
// ----------------------------------------------------------------------------

public interface IBehavior
{
    // Higher number = more important. Dispatcher picks the highest-priority
    // behavior whose WouldAct returned true.
    int Priority { get; }

    // Would this behavior act right now? Caches what Act needs on the instance.
    // Pure-ish: reads world state, may roll RNG when the roll IS the decision.
    bool WouldAct(int entityId);

    // Commit the side effect, trusting the cached decision from WouldAct.
    // Only called when WouldAct returned true AND this behavior won priority.
    // Returns the action's cost in periods (1 = baseline, higher = slower).
    // Same behavior can return different costs depending on what it did — e.g.
    // HuntBehavior returns 1 for a step and 2 for a bite.
    int Act(int entityId);
}

// An entity's composed behaviors. Order doesn't matter — priority picks the winner.
public class Behaviors
{
    public List<IBehavior> list;

    public Behaviors(params IBehavior[] behaviors)
    {
        list = new List<IBehavior>(behaviors);
    }
}
