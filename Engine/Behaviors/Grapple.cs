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

    // ----------------------------------------------------------------------------
    // Probability the victim breaks free on an escape attempt: victim Agility vs
    // attacker Strength, with Strength weighted 3× — a strong predator genuinely
    // pins its prey. At Str 80 vs Agi 70, chance ≈ 0.23.
    // ----------------------------------------------------------------------------
    public double EscapeChance(int victimId)
    {
        int str = StatMath.Require(attackerId).Strength;
        int agi = StatMath.Require(victimId).Agility;
        return (double)agi / (agi + 3 * str);
    }
}

// Marker: a Behavior that can still run while its entity is grappled. The
// dispatcher filters non-ICanActWhenGrappled behaviors out of the priority
// comparison when the entity has Grappled. Today only EscapeGrappleBehavior
// qualifies; future while-pinned skills (cast, poke-eyes, etc.) will too.
public interface ICanActWhenGrappled { }

// Behavior: try to break free of a grapple. Success chance comes from
// StatMath.EscapeChance (victim's Agility vs attacker's Strength). On
// success: detach Grappled, step one cell away. On failure: turn wasted,
// still pinned.
// High priority so that once other while-grappled skills exist, escape remains
// the default when nothing fancier claims the tick.
public class EscapeGrappleBehavior : IBehavior, ICanActWhenGrappled
{
    public int Priority => 100;

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
    // Roll the escape against attacker Strength vs victim Agility — the Grappled
    // state owns the formula. Success: detach Grappled and step one cell away
    // from the attacker. Failure: log the struggle and burn the turn. Cost 1.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Grappled g = World.GetComponent<Grappled>(id);
        int attackerId = g.attackerId;

        if (rng.NextDouble() >= g.EscapeChance(id))
        {
            World.Log($"{World.Label(id)} struggles but stays pinned");
            return 1;
        }

        World.DetachComponent<Grappled>(id);

        Position pos = World.GetComponent<Position>(id);
        if (World.EntityExists(attackerId) && World.HasComponent<Position>(attackerId))
        {
            Position attackerPos = World.GetComponent<Position>(attackerId);
            MovementHelper.MoveAwayFrom(id, pos, attackerPos.X, attackerPos.Y, rng);
        }

        World.Log($"{World.Label(id)} breaks free");
        return 1;
    }
}
