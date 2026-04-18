namespace Engine;

// Behavior: run from the nearest creature whose prey list includes my species.
// No dedicated "Prey" marker — an entity qualifies as prey by being on someone's
// Predator.preySpecies. That way one source of truth decides the wolf↔rabbit pairing.
public class FleeBehavior : IBehavior
{
    public int Priority => 100;

    // Cached between WouldAct and Act — which threat to flee from.
    private int cachedThreatId = -1;

    // ----------------------------------------------------------------------------
    // Look for the nearest predator whose hunt list contains this entity's species.
    // Cache it for Act.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Sensing>(id) || !World.HasComponent<Species>(id) || !World.HasComponent<Position>(id)) return false;

        Species species = World.GetComponent<Species>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedThreatId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id
            && World.HasComponent<Predator>(other)
            && World.GetComponent<Predator>(other).Hunts(species.spawn));

        return cachedThreatId >= 0;
    }

    // ----------------------------------------------------------------------------
    // Step one cell away from the cached threat. Cost 1 — panic is quick.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);
        Position threatPos = World.GetComponent<Position>(cachedThreatId);
        MovementHelper.MoveAwayFrom(id, pos, threatPos.X, threatPos.Y);
        return 1;
    }
}
