namespace Engine;

// State: where an entity is on the grid. Immutable — only World can change a
// position (via MoveEntity), which keeps the spatial index in sync.
public class Position
{
    public int X { get; }
    public int Y { get; }

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }
}

// State: creatures and items can walk here.
public class Walkable { }

// State: this entity occupies its cell. Blocks other Solids from entering —
// two creatures can't stack. Corpses, plants, and ground don't have it.
public class Solid { }

// ----------------------------------------------------------------------------
// Helpers for moving entities on the grid. Behaviors call these directly.
// ----------------------------------------------------------------------------
public static class MovementHelper
{
    private static readonly (int dx, int dy)[] directions = { (0, -1), (0, 1), (1, 0), (-1, 0) };

    // ----------------------------------------------------------------------------
    // Try to step one cell. Needs walkable ground AND no Solid occupant at the target.
    // Returns false if blocked, occupied, or out of bounds.
    // ----------------------------------------------------------------------------
    public static bool TryMove(int id, int dx, int dy)
    {
        if (!World.HasComponent<Position>(id)) return false;

        Position pos = World.GetComponent<Position>(id);
        int targetX = pos.X + dx;
        int targetY = pos.Y + dy;

        if (targetX < 0 || targetX >= World.mapWidth || targetY < 0 || targetY >= World.mapHeight)
            return false;

        // Scan target cell: need walkable ground, and bail if any other Solid already sits there
        bool hasWalkable = false;
        foreach (int other in World.EntitiesAt(targetX, targetY))
        {
            if (other == id) continue;
            if (World.HasComponent<Solid>(other)) return false;
            if (World.HasComponent<Walkable>(other)) hasWalkable = true;
        }

        if (!hasWalkable) return false;

        World.MoveEntity(id, targetX, targetY);
        return true;
    }

    // ----------------------------------------------------------------------------
    // Step one cell closer to the target. Prefer the bigger axis; if that step is
    // blocked, fall through to the other axis. The fallback keeps movers from
    // stalling when the preferred direction is blocked by another Solid or a wall.
    // ----------------------------------------------------------------------------
    public static void MoveToward(int id, Position pos, int targetX, int targetY)
    {
        int dx = Math.Sign(targetX - pos.X);
        int dy = Math.Sign(targetY - pos.Y);

        // Pick the axis we want to try first, then fall through to the other if blocked
        bool preferX = Math.Abs(targetX - pos.X) >= Math.Abs(targetY - pos.Y);
        if (preferX)
        {
            if (dx != 0 && TryMove(id, dx, 0)) return;
            if (dy != 0) TryMove(id, 0, dy);
        }
        else
        {
            if (dy != 0 && TryMove(id, 0, dy)) return;
            if (dx != 0) TryMove(id, dx, 0);
        }
    }

    // ----------------------------------------------------------------------------
    // Step one cell toward the neighbor that's furthest from the threat. Tries
    // all four cardinals, rejects any that aren't passable, picks the one with
    // largest squared-Euclidean distance from the threat. Fixes the "run to wall,
    // stop" bug — if east is blocked, the fleer picks north or south instead.
    // ----------------------------------------------------------------------------
    public static void MoveAwayFrom(int id, Position pos, int threatX, int threatY)
    {
        int bestScore = int.MinValue;
        (int dx, int dy) bestStep = (0, 0);
        bool found = false;

        foreach ((int dx, int dy) offset in directions)
        {
            int nx = pos.X + offset.dx;
            int ny = pos.Y + offset.dy;
            if (!World.IsCreatureSpawnable(nx, ny)) continue;

            int ddx = nx - threatX;
            int ddy = ny - threatY;
            int score = ddx * ddx + ddy * ddy;
            if (score > bestScore)
            {
                bestScore = score;
                bestStep = offset;
                found = true;
            }
        }

        if (found) TryMove(id, bestStep.dx, bestStep.dy);
    }

    // ----------------------------------------------------------------------------
    // Step in a random direction. Caller supplies the rng for determinism.
    // ----------------------------------------------------------------------------
    public static void Wander(int id, Random rng)
    {
        var dir = directions[rng.Next(directions.Length)];
        TryMove(id, dir.dx, dir.dy);
    }
}
