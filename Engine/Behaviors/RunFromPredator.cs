namespace Engine;

// Behavior: reflex. Run from the nearest creature whose prey list includes my
// species. No dedicated "Prey" marker — an entity qualifies as prey by being on
// someone's Predator.preySpecies. That way one source of truth decides the
// wolf↔rabbit pairing.
// Named for the reflex: the entity runs when it sees a predator. Reserve the
// generic verb "Flee" for a future player-callable skill — distinct concept.
public class RunFromPredatorBehavior : IBehavior
{
    public int Priority => 100;

    private Random rng;

    // Cached between WouldAct and Act — the fleeing step direction.
    private int cachedStepDx;
    private int cachedStepDy;

    public RunFromPredatorBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Read the shared per-tick flow field of predators that hunt this entity's
    // species. FlowFieldHelper picks the neighbor that's FURTHEST from the
    // nearest threat (mirror of Hunt's nearest-step pick). The reflex only
    // fires when a threat is within vision — the helper returns false
    // otherwise, and Wander/Rest handle the idle case.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Species>(id) || !World.HasComponent<Position>(id)) return false;

        Species species = World.GetComponent<Species>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = StatMath.VisionRange(id);

        List<FlowField> fields = new List<FlowField> { World.GetPredatorsHuntingFlowField(species.spawn) };

        if (!FlowFieldHelper.PickFarthestNeighborStep(
                pos.X, pos.Y, fields, World.CanCreatureBeHere, range, rng,
                out FlowFieldStep step))
            return false;

        cachedStepDx = step.stepDx;
        cachedStepDy = step.stepDy;
        return true;
    }

    // ----------------------------------------------------------------------------
    // Step one cell along the cached flee direction. Cost 1 — a reflex step is quick.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }
}
