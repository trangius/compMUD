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
    //
    // If rng is supplied, neighbor-iteration order is shuffled once per call.
    // That breaks the cardinal-first bias in cameFrom when several equal-length
    // paths reach the same cell — without it, many callers flooding toward the
    // same goal all pick identical FirstStep directions and form straight lines.
    // ----------------------------------------------------------------------------
    public static BFSResult BFS(int startX, int startY, int maxRange, Func<int, int, bool> isPassable, Random? rng = null)
    {
        BFSResult result = new BFSResult(startX, startY);

        // Local copy of the 8 directions; shuffled below if rng was provided.
        (int dx, int dy)[] dirs = (( int dx, int dy)[])directions.Clone();
        if (rng != null)
        {
            // Fisher-Yates — one pass, reorders this BFS call's neighbor scan.
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }
        }

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

            foreach ((int dx, int dy) dir in dirs)
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

    // ----------------------------------------------------------------------------
    // Multi-source BFS — flood outward from many seed cells at once, through
    // cells where isPassable returns true. Returns a FlowField: every reached
    // cell knows its distance to the NEAREST seed and the direction one step
    // toward that seed.
    //
    // Seed cells themselves skip the passability test — same exemption BFS
    // above makes for the start cell. So a prey cell (Solid) can be a seed;
    // the flood propagates from it into passable neighbors but never re-enters
    // the seed. Seeds get distance 0 and stepToward (0,0).
    //
    // No maxRange parameter — consumers apply their own vision check against
    // the per-cell distance. The point of this algorithm is one flood per tick
    // covering every consumer, so bounding by any single consumer's vision
    // would defeat the purpose.
    // ----------------------------------------------------------------------------
    public static FlowField MultiSourceBFS(IEnumerable<(int x, int y)> sources, Func<int, int, bool> isPassable)
    {
        FlowField result = new FlowField();

        // Seed every source at distance 0. Multiple sources share the queue —
        // BFS resolves ties at a cell by whichever source reaches it first,
        // which is also the nearest in step count.
        Queue<(int x, int y)> frontier = new Queue<(int, int)>();
        foreach ((int x, int y) src in sources)
        {
            if (result.distance.ContainsKey(src)) continue;  // duplicate source cell
            result.distance[src] = 0;
            result.stepToward[src] = (0, 0);
            frontier.Enqueue(src);
        }

        // Standard BFS. stepToward[N] = direction from N one step back to the
        // parent C (N's predecessor on the path from N back to the nearest
        // source). That's (Cx - Nx, Cy - Ny), always a unit 8-direction.
        while (frontier.Count > 0)
        {
            (int x, int y) cell = frontier.Dequeue();
            int nextDist = result.distance[(cell.x, cell.y)] + 1;

            foreach ((int dx, int dy) dir in directions)
            {
                int nx = cell.x + dir.dx;
                int ny = cell.y + dir.dy;
                (int, int) key = (nx, ny);

                if (result.distance.ContainsKey(key)) continue;
                if (!isPassable(nx, ny)) continue;

                result.distance[key] = nextDist;
                result.stepToward[key] = (cell.x - nx, cell.y - ny);
                frontier.Enqueue((nx, ny));
            }
        }

        return result;
    }
}

// ----------------------------------------------------------------------------
// Output of a multi-source BFS. For every reachable cell:
//   distance  = steps to the nearest seed
//   stepToward = the one-cell unit direction you'd take from here to move
//                closer to that nearest seed
// Seed cells themselves have distance 0 and stepToward (0, 0).
// ----------------------------------------------------------------------------
public class FlowField
{
    public Dictionary<(int x, int y), int> distance = new Dictionary<(int, int), int>();
    public Dictionary<(int x, int y), (int dx, int dy)> stepToward = new Dictionary<(int, int), (int, int)>();

    public bool Reachable(int x, int y) => distance.ContainsKey((x, y));
    public int Distance(int x, int y) => distance[(x, y)];
    public (int dx, int dy) StepToward(int x, int y) => stepToward[(x, y)];
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
