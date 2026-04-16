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
    // Step one cell closer to the target.
    // ----------------------------------------------------------------------------
    public static void MoveToward(int id, Position pos, int targetX, int targetY)
    {
        int dx = Math.Sign(targetX - pos.X);
        int dy = Math.Sign(targetY - pos.Y);

        if (Math.Abs(targetX - pos.X) >= Math.Abs(targetY - pos.Y))
            TryMove(id, dx, 0);
        else
            TryMove(id, 0, dy);
    }

    // ----------------------------------------------------------------------------
    // Step one cell away from the threat. Solid prevents same-cell, so at least
    // one of dx/dy is non-zero when this runs.
    // ----------------------------------------------------------------------------
    public static void MoveAwayFrom(int id, Position pos, int threatX, int threatY)
    {
        int dx = Math.Sign(pos.X - threatX);
        int dy = Math.Sign(pos.Y - threatY);

        if (dx != 0)
            TryMove(id, dx, 0);
        else if (dy != 0)
            TryMove(id, 0, dy);
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
