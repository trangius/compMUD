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
    // 8-connected step offsets: 4 cardinals + 4 diagonals. Diagonals cost the
    // same as cardinals in BFS and movement — minor "distance cheat" on long
    // paths, in exchange for organic-looking clusters and movement paths.
    private static readonly (int dx, int dy)[] directions = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

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
    // Step one cell closer to the target. When both axes want progress we try the
    // diagonal step first (8-connected movement makes that the shortest path).
    // If the diagonal is blocked, fall through to single-axis moves — bigger
    // component first, then the other. Keeps movers from stalling when any one
    // direction is blocked by a Solid or a wall.
    // ----------------------------------------------------------------------------
    public static void MoveToward(int id, Position pos, int targetX, int targetY)
    {
        int dx = Math.Sign(targetX - pos.X);
        int dy = Math.Sign(targetY - pos.Y);

        // Both axes want progress — take a diagonal step if we can
        if (dx != 0 && dy != 0 && TryMove(id, dx, dy)) return;

        // Single-axis fallback: bigger component first, then the other
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
    // all 8 neighbors, rejects any that aren't passable, picks the one with
    // largest squared-Euclidean distance from the threat. Fixes the "run to wall,
    // stop" bug — if east is blocked, the fleer picks a diagonal or the other axis.
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
