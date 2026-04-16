namespace Engine;

// State: this entity is prey. Used by HuntBehavior to find targets.
public class Flees { }

// Behavior: run from the nearest predator.
public class FleeBehavior : IBehavior
{
    public int Priority => 100;

    // Cached between WouldAct and Act — which threat to flee from.
    private int cachedThreatId = -1;

    // ----------------------------------------------------------------------------
    // Look for the nearest predator. Cache it for Act.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Position>(id)) return false;

        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedThreatId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id && World.HasComponent<Hunts>(other));

        return cachedThreatId >= 0;
    }

    // ----------------------------------------------------------------------------
    // Step one cell away from the cached threat.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);
        Position threatPos = World.GetComponent<Position>(cachedThreatId);
        MovementHelper.MoveAwayFrom(id, pos, threatPos.X, threatPos.Y);
    }
}
