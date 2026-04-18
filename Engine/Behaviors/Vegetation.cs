namespace Engine;

// State: living vegetation that spreads to nearby cells and occasionally spawns
// new plants elsewhere. Species identity lives on the separate Species component;
// Vegetation only carries growth rates and the cluster cap. clusterCap +
// clusterRadius put a ceiling on cluster density — once a neighborhood is
// saturated, new growth has to go elsewhere.
public class Vegetation
{
    public double spreadChance = 0.01;
    public double spawnChance = 0.0;
    public int clusterCap = int.MaxValue;  // max same-species count tolerated in target's neighborhood
    public int clusterRadius = 2;          // Chebyshev radius that defines "neighborhood"
}

// Behavior: try to spread to an adjacent open cell, or spawn at a random open cell.
public class GrowBehavior : IBehavior
{
    public int Priority => 10;

    private Random rng;

    // Cached between WouldAct and Act — where to drop the clone.
    private int cachedTargetX;
    private int cachedTargetY;

    // 8-connected spread: plants drop seeds into any neighbor cell, diagonals
    // included. Keeps clusters blobby instead of growing into cardinal-only lines.
    private static readonly (int dx, int dy)[] directions = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

    public GrowBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Roll for spread first (try an adjacent cell). If that misses, roll for spawn
    // (try a random cell). The roll IS the decision — the outcome is cached.
    // Each candidate must be open ground AND have room (local cluster not saturated).
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Vegetation>(id) || !World.HasComponent<Species>(id) || !World.HasComponent<Position>(id)) return false;

        Vegetation veg = World.GetComponent<Vegetation>(id);
        Species species = World.GetComponent<Species>(id);
        Position pos = World.GetComponent<Position>(id);

        // First try: spread to an adjacent cell
        if (rng.NextDouble() < veg.spreadChance)
        {
            (int dx, int dy) dir = directions[rng.Next(directions.Length)];
            int nx = pos.X + dir.dx;
            int ny = pos.Y + dir.dy;

            if (World.IsOpenGround(nx, ny) && HasRoom(nx, ny, veg, species, id))
            {
                cachedTargetX = nx;
                cachedTargetY = ny;
                return true;
            }
            // Roll succeeded but target was blocked or saturated — fall through and try spawn
        }

        // Second try: spawn at a random open cell anywhere on the map
        if (rng.NextDouble() < veg.spawnChance)
        {
            int x = rng.Next(2, World.mapWidth - 2);
            int y = rng.Next(2, World.mapHeight - 2);

            if (World.IsOpenGround(x, y) && HasRoom(x, y, veg, species, id))
            {
                cachedTargetX = x;
                cachedTargetY = y;
                return true;
            }
        }

        return false;
    }

    // ----------------------------------------------------------------------------
    // Is the target cell's neighborhood below the species' cluster cap? Positive
    // check — we ask "room remaining?", not "am I blocked?". Delegates the
    // species-match counting to Species.CountInRadius.
    // ----------------------------------------------------------------------------
    private static bool HasRoom(int tx, int ty, Vegetation veg, Species species, int selfId)
    {
        if (veg.clusterCap == int.MaxValue) return true;
        return Species.CountInRadius(tx, ty, veg.clusterRadius, species.spawn, selfId) < veg.clusterCap;
    }

    // ----------------------------------------------------------------------------
    // Spawn a fresh plant of the same species at the cached target cell.
    // Cost 1 — a spread is one plant-action.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Species species = World.GetComponent<Species>(id);
        species.spawn(cachedTargetX, cachedTargetY);
        return 1;
    }
}
