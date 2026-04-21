namespace Engine;

// ----------------------------------------------------------------------------
// Pursuit: walk to a fixed cell. Completes when the entity arrives.
//
// Used for stationary targets — camp, a specific corpse, a specific bush.
// NOT used for moving or fungible targets ("a rabbit"); those stay reactive
// because the target moves and re-evaluating each tick is the right call.
//
// Step reads a per-cell flow field (World.GetCellFlowField), seeded at the
// goal. The flood is cached for the tick and shared across everyone heading
// to the same goal cell — two hunters returning to the same camp pay one
// flood between them, not two.
// ----------------------------------------------------------------------------
public class NavigatePursuit : IPursuit
{
    public int Priority { get; }
    public int goalX;
    public int goalY;

    // Rng drives tie-breaks when several neighbor cells are equally close to
    // the goal — without it, crowds heading to the same destination funnel
    // through one cell and stack up.
    private Random rng;

    public NavigatePursuit(int x, int y, int priority, Random rng)
    {
        goalX = x;
        goalY = y;
        Priority = priority;
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Arrived when the entity's own cell matches the goal cell. If the entity
    // has no Position treat it as complete — nothing sensible to do otherwise.
    // ----------------------------------------------------------------------------
    public bool IsComplete(int id)
    {
        if (!World.HasComponent<Position>(id)) return true;
        Position pos = World.GetComponent<Position>(id);
        return pos.X == goalX && pos.Y == goalY;
    }

    // ----------------------------------------------------------------------------
    // Take one step toward the goal via the shared cell flow field. The
    // entity's own cell is Solid so it's never in the field; FlowFieldHelper
    // scans the 8 neighbors and picks the one closest to the goal. If the
    // goal is unreachable from here, no neighbor shows a distance and the
    // step no-ops at cost 1 — the pursuit will sit and try again next tick.
    // Caller is responsible for not attaching a hopeless pursuit.
    // ----------------------------------------------------------------------------
    public int Step(int id)
    {
        if (!World.HasComponent<Position>(id)) return 1;
        Position pos = World.GetComponent<Position>(id);

        List<FlowField> fields = new List<FlowField> { World.GetCellFlowField(goalX, goalY) };
        if (!FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, int.MaxValue, rng, out FlowFieldStep step))
            return 1;

        MovementHelper.TryMove(id, step.stepDx, step.stepDy);
        return 1;
    }
}
