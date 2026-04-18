namespace Engine;

// ----------------------------------------------------------------------------
// Generic grid algorithms — domain-agnostic flood / pathfinding utilities.
// Callers pass the passability predicate so the engine doesn't bake in one
// definition of "walkable" (a wolf wading through water may differ from a rabbit).
// ----------------------------------------------------------------------------
public static class Algorithms
{
    // 8-connected step offsets. Matches MovementHelper.directions — diagonals
    // expand at the same cost as cardinals, so BFS returns Chebyshev-distance paths.
    private static readonly (int dx, int dy)[] directions = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

    // ----------------------------------------------------------------------------
    // Flood outward from (startX, startY) by BFS, up to maxRange steps, through
    // cells where isPassable returns true. The start cell itself is always marked
    // reachable at distance 0 — we don't test it, because the caller typically
    // stands on it and may not pass the predicate (e.g. a Solid creature).
    // ----------------------------------------------------------------------------
    public static BFSResult BFS(int startX, int startY, int maxRange, Func<int, int, bool> isPassable)
    {
        BFSResult result = new BFSResult(startX, startY);

        // Seed: start cell sits at distance 0 with no parent.
        result.distance[(startX, startY)] = 0;
        Queue<(int x, int y)> frontier = new Queue<(int, int)>();
        frontier.Enqueue((startX, startY));

        // Standard BFS — pop a cell, push each unvisited passable neighbor.
        while (frontier.Count > 0)
        {
            (int x, int y) cell = frontier.Dequeue();
            int nextDist = result.distance[(cell.x, cell.y)] + 1;
            if (nextDist > maxRange) continue;

            foreach ((int dx, int dy) dir in directions)
            {
                int nx = cell.x + dir.dx;
                int ny = cell.y + dir.dy;
                (int, int) key = (nx, ny);

                // Skip if already visited or not passable for this caller
                if (result.distance.ContainsKey(key)) continue;
                if (!isPassable(nx, ny)) continue;

                result.distance[key] = nextDist;
                result.cameFrom[key] = (cell.x, cell.y);
                frontier.Enqueue((nx, ny));
            }
        }

        return result;
    }
}

// ----------------------------------------------------------------------------
// Output of a BFS flood. `distance` tells how many steps to each reached cell;
// `cameFrom` names each cell's parent on the shortest path back to start.
// Together they let FirstStep reconstruct the next move toward any goal cell.
// ----------------------------------------------------------------------------
public class BFSResult
{
    public Dictionary<(int x, int y), int> distance = new Dictionary<(int, int), int>();
    public Dictionary<(int x, int y), (int x, int y)> cameFrom = new Dictionary<(int, int), (int, int)>();
    private int startX { get; }
    private int startY { get; }

    public BFSResult(int startX, int startY)
    {
        this.startX = startX;
        this.startY = startY;
    }

    // ----------------------------------------------------------------------------
    // Did the flood reach (x, y) within maxRange?
    // ----------------------------------------------------------------------------
    public bool Reachable(int x, int y)
    {
        return distance.ContainsKey((x, y));
    }

    // ----------------------------------------------------------------------------
    // Shortest-path step count from start to (x, y). Caller must check Reachable first.
    // ----------------------------------------------------------------------------
    public int Distance(int x, int y)
    {
        return distance[(x, y)];
    }

    // ----------------------------------------------------------------------------
    // First move the walker should take to head along the shortest path to
    // (goalX, goalY). Returns (0, 0) if the goal is the start cell or unreachable.
    // Walks cameFrom backward from the goal until we find the step off of start.
    // ----------------------------------------------------------------------------
    public (int dx, int dy) FirstStep(int goalX, int goalY)
    {
        (int x, int y) cell = (goalX, goalY);
        if (!cameFrom.ContainsKey((cell.x, cell.y))) return (0, 0);

        // Trace parents backward. The step we want is the one whose parent IS start.
        while (true)
        {
            (int x, int y) parent = cameFrom[(cell.x, cell.y)];
            if (parent.x == startX && parent.y == startY)
                return (cell.x - startX, cell.y - startY);
            cell = parent;
        }
    }
}
