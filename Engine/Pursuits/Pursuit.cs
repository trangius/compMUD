namespace Engine;

// ----------------------------------------------------------------------------
// A persistent multi-tick intent. Where IBehavior is reactive ("what do I
// want to do this tick?"), IPursuit is committed ("I'm walking home, don't
// re-decide each step"). Same shape as IBehavior — Priority is on the
// interface so the symmetry is visible at the call site.
//
// Concrete pursuits listed here for scope visibility; only NavigatePursuit
// ships today. The others earn their keep when a real caller appears:
//
//   WaitPursuit(duration)   — stand here N ticks. Stall-keeper, guard, rest.
//   PatrolPursuit(waypoints)— walk a fixed loop.
//   FollowPursuit(targetId) — stay adjacent to a moving entity.
//   CraftingPursuit(recipe) — execute ONE recipe at a workstation. Distinct
//                             from a "build a cabin" quest, which weaves
//                             many recipes together at a higher layer.
//
// Rule of thumb for future additions: one process = one pursuit. Orchestrating
// many processes over time is a quest (separate, named-NPCs-only layer).
// ----------------------------------------------------------------------------
public interface IPursuit
{
    // How hard this pursuit is to interrupt. A reactive behavior must beat
    // this priority to preempt. Set at construction by the caller; different
    // instances of the same pursuit type can have different urgencies (a
    // firefighter's NavigatePursuit to a fire runs at priority 90, a
    // hunter's NavigatePursuit home runs at priority 3).
    int Priority { get; }

    // Am I done? Checked at the start of the entity's turn; if true, the
    // dispatcher detaches the Pursuit before reactive evaluation.
    bool IsComplete(int entityId);

    // Advance one tick. Returns action cost (same meaning as IBehavior.Act —
    // the scheduler multiplies by the entity's action period).
    int Step(int entityId);
}

// ----------------------------------------------------------------------------
// State: this entity is pursuing one ongoing goal. Holds a single IPursuit.
// The wrapper's only job is addressability — "does this entity have a
// pursuit, and if so, which one?". Priority delegates through.
//
// Attach via `World.AttachComponent(id, new Pursuit(new NavigatePursuit(x, y, priority: 3)))`.
// Dispatcher detaches automatically when IsComplete turns true.
//
// Only one active pursuit at a time by design — the hunter walks home OR
// walks to a corpse, not both. If the reactive layer wants to change goals,
// AttachComponent on Pursuit replaces (which fires OnDetach of the old).
// ----------------------------------------------------------------------------
public class Pursuit
{
    public IPursuit current;
    public int Priority => current.Priority;

    public Pursuit(IPursuit p)
    {
        current = p;
    }
}
