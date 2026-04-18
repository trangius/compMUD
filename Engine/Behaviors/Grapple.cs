namespace Engine;

// State: this entity is pinned by another — held adjacent, unable to take
// normal actions. While this component is attached, the dispatcher runs only
// behaviors that implement ICanActWhenGrappled. Auto-released by the dispatcher
// at the victim's turn if the attacker is no longer alive or adjacent — keeps
// the state honest without needing a wall-clock effect.
public class Grappled
{
    public int attackerId;

    // ----------------------------------------------------------------------------
    // Is the grapple still meaningful? True if the attacker exists and is
    // Chebyshev-adjacent (8-connected) to the victim. Dispatcher calls this at
    // the victim's turn; a false result triggers auto-detach.
    // ----------------------------------------------------------------------------
    public bool IsStillValid(int victimId)
    {
        if (!World.EntityExists(attackerId)) return false;
        if (!World.HasComponent<Position>(attackerId)) return false;
        if (!World.HasComponent<Position>(victimId)) return false;

        Position vp = World.GetComponent<Position>(victimId);
        Position ap = World.GetComponent<Position>(attackerId);
        return Math.Max(Math.Abs(vp.X - ap.X), Math.Abs(vp.Y - ap.Y)) <= 1;
    }
}

// Marker: a Behavior that can still run while its entity is grappled. The
// dispatcher filters non-ICanActWhenGrappled behaviors out of the priority
// comparison when the entity has Grappled. Today only EscapeGrappleBehavior
// qualifies; future while-pinned skills (cast, poke-eyes, etc.) will too.
public interface ICanActWhenGrappled { }

// Behavior: try to break free of a grapple. Rolls a flat success chance per
// attempt. On success: detach Grappled, step one cell away from the attacker.
// On failure: the turn is wasted (still pinned, no movement).
// High priority so that once other while-grappled skills exist, escape remains
// the default when nothing fancier claims the tick.
public class EscapeGrappleBehavior : IBehavior, ICanActWhenGrappled
{
    public int Priority => 100;

    // Tunable per-attempt probability. Flat for now; will hook into a future
    // Strength comparison between attacker and victim when stats land.
    public double escapeChance = 0.2;

    private Random rng;

    public EscapeGrappleBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Willing whenever grappled. Dispatcher has already filtered other behaviors.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        return World.HasComponent<Grappled>(id);
    }

    // ----------------------------------------------------------------------------
    // Roll the escape. Success: detach Grappled and step one cell away from the
    // attacker. Failure: log the struggle and burn the turn. Cost 1 either way.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (rng.NextDouble() >= escapeChance)
        {
            World.Log($"{World.GetEntityName(id)} struggles but stays pinned");
            return 1;
        }

        // Capture attacker position before detaching — we still need it to flee
        Grappled g = World.GetComponent<Grappled>(id);
        int attackerId = g.attackerId;
        World.DetachComponent<Grappled>(id);

        Position pos = World.GetComponent<Position>(id);
        if (World.EntityExists(attackerId) && World.HasComponent<Position>(attackerId))
        {
            Position attackerPos = World.GetComponent<Position>(attackerId);
            MovementHelper.MoveAwayFrom(id, pos, attackerPos.X, attackerPos.Y);
        }

        World.Log($"{World.GetEntityName(id)} breaks free");
        return 1;
    }
}
