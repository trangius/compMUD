namespace Engine;

// State: living vegetation that spreads to nearby cells and occasionally spawns
// new plants elsewhere. The spawn function points at the plant's archetype so
// each sprout is freshly built. localCap + localRadius put a ceiling on cluster
// density — once a neighborhood is saturated, new growth has to go elsewhere.
public class Vegetation
{
    public double spreadChance = 0.01;
    public double spawnChance = 0.0;
    public int localCap = int.MaxValue;  // max same-species count tolerated in target's neighborhood
    public int localRadius = 2;          // Chebyshev radius that defines "neighborhood"
    public required Func<int, int, int> spawn;  // archetype's Create method for this plant
}

// Behavior: try to spread to an adjacent open cell, or spawn at a random open cell.
public class GrowBehavior : IBehavior
{
    public int Priority => 10;

    private Random rng;

    // Cached between WouldAct and Act — where to drop the clone.
    private int cachedTargetX;
    private int cachedTargetY;

    private static readonly (int dx, int dy)[] directions = { (0, -1), (0, 1), (1, 0), (-1, 0) };

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
        if (!World.HasComponent<Vegetation>(id) || !World.HasComponent<Position>(id)) return false;

        Vegetation veg = World.GetComponent<Vegetation>(id);
        Position pos = World.GetComponent<Position>(id);

        // First try: spread to an adjacent cell
        if (rng.NextDouble() < veg.spreadChance)
        {
            (int dx, int dy) dir = directions[rng.Next(directions.Length)];
            int nx = pos.X + dir.dx;
            int ny = pos.Y + dir.dy;

            if (World.IsOpenGround(nx, ny) && HasRoom(nx, ny, veg, id))
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

            if (World.IsOpenGround(x, y) && HasRoom(x, y, veg, id))
            {
                cachedTargetX = x;
                cachedTargetY = y;
                return true;
            }
        }

        return false;
    }

    // ----------------------------------------------------------------------------
    // Is the target cell's neighborhood below the species' local cap? Counts same-
    // species vegetation within localRadius of (tx, ty). Positive check — we want
    // to see "room remaining", not "am I blocked by too many bushes".
    // ----------------------------------------------------------------------------
    private static bool HasRoom(int tx, int ty, Vegetation veg, int selfId)
    {
        if (veg.localCap == int.MaxValue) return true;

        int count = 0;
        for (int dx = -veg.localRadius; dx <= veg.localRadius; dx++)
        {
            for (int dy = -veg.localRadius; dy <= veg.localRadius; dy++)
            {
                foreach (int other in World.EntitiesAt(tx + dx, ty + dy))
                {
                    if (other == selfId) continue;
                    if (!World.HasComponent<Vegetation>(other)) continue;
                    if (World.GetComponent<Vegetation>(other).spawn == veg.spawn) count++;
                }
            }
        }
        return count < veg.localCap;
    }

    // ----------------------------------------------------------------------------
    // Spawn a fresh plant of the same species at the cached target cell.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Vegetation veg = World.GetComponent<Vegetation>(id);
        veg.spawn(cachedTargetX, cachedTargetY);
    }
}
