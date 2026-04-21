namespace Engine;

// ----------------------------------------------------------------------------
// Stress test area — large open pasture packed with wolves and rabbits to
// measure tick cost. No pond, no forest, just bordered grass with sparse
// bushes for food. Wolves have RaidingWolf detached so they don't return-and-
// despawn after a kill — keeps the predator population steady so the load
// stays representative.
// ----------------------------------------------------------------------------
public static class StressArea
{
    // ----------------------------------------------------------------------------
    // Build the test world. Caller chooses how many of each.
    // ----------------------------------------------------------------------------
    public static void Build(int rabbits, int wolves, int bushes, int seed = 42)
    {
        Random rng = new Random(seed);
        int width = World.mapWidth;
        int height = World.mapHeight;

        // Bordered grass — walls on the edges, grass everywhere inside
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    Archetypes.CreateWall(x, y);
                else
                    Archetypes.CreateGrass(x, y);
            }
        }

        // Bushes — uniform scatter so rabbits have something to feed on.
        // Without food the rabbit pop crashes and the test stops being a stress test.
        for (int i = 0; i < bushes; i++)
        {
            int x = rng.Next(2, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateBush(x, y);
        }

        // Rabbits — use FindCell so we never stack them on the same tile.
        for (int i = 0; i < rabbits; i++)
        {
            (int rx, int ry) = World.FindCell((cx, cy) => World.CanCreatureBeHere(cx, cy), rng);
            if (rx < 0) break;
            int id = Archetypes.CreateRabbit(rx, ry);
            World.GetComponent<Energy>(id).SetCurrent(1000);
        }

        // Wolves — same placement rule. Detach RaidingWolf so ReturnToForest
        // never fires and the wolf keeps hunting forever.
        for (int i = 0; i < wolves; i++)
        {
            (int wx, int wy) = World.FindCell((cx, cy) => World.CanCreatureBeHere(cx, cy), rng);
            if (wx < 0) break;
            int id = Archetypes.CreateWolf(wx, wy);
            World.DetachComponent<RaidingWolf>(id);
        }
    }
}
