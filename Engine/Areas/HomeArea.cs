namespace Engine;

// ----------------------------------------------------------------------------
// Area builders — concrete worlds you can pour into an initialized World.
// Each method builds terrain + entities for one area. The engine doesn't know
// about any of them. Future areas slot in as more methods on this class.
// ----------------------------------------------------------------------------
public static class HomeArea
{
    // ----------------------------------------------------------------------------
    // The starting area — walled pasture with a pond, scattered trees and bushes,
    // a handful of rabbits and wolves. This is what frontends build by default.
    // ----------------------------------------------------------------------------
    public static void StartingArea(int seed = 42)
    {
        Random rng = new Random(seed);
        int width = World.mapWidth;
        int height = World.mapHeight;

        // Walls around the border, grass everywhere else
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    Archetypes.CreateWall(x, y);
                    continue;
                }
                Archetypes.CreateGrass(x, y);
            }
        }

        // Carve a pond in the middle
        int pondCenterX = width / 2;
        int pondCenterY = height / 2;
        int pondRadius = Math.Min(width, height) / 8;
        for (int x = pondCenterX - pondRadius; x <= pondCenterX + pondRadius; x++)
        {
            for (int y = pondCenterY - pondRadius; y <= pondCenterY + pondRadius; y++)
            {
                int dx = x - pondCenterX;
                int dy = y - pondCenterY;
                if (dx * dx + dy * dy <= pondRadius * pondRadius + rng.Next(-2, 3))
                {
                    // Replace grass with water
                    foreach (int existing in World.EntitiesAt(x, y))
                    {
                        if (World.HasComponent<Walkable>(existing))
                            World.DestroyEntity(existing);
                    }
                    Archetypes.CreateWater(x, y);
                }
            }
        }

        // Trees — two tight forest clusters in the NW (wolves emerge from and
        // retreat to these cells), plus a few scattered trees across the rest.
        int treeCount = (width * height) / 20;
        int forestTrees = treeCount * 2 / 3;
        int scatteredTrees = treeCount - forestTrees;

        // Pick forest centers inside the NW third. Each tree will drop near one
        // of these, giving a packed-middle / sparse-edge blob per cluster.
        int forestRadius = Math.Min(width, height) / 14;
        List<(int cx, int cy)> forestCenters = new List<(int, int)>();
        for (int i = 0; i < 2; i++)
            forestCenters.Add((
                rng.Next(forestRadius + 2, width / 3),
                rng.Next(forestRadius + 2, height * 2 / 3)));

        // Drop trees around the centers. Averaging two offsets gives a
        // triangular distribution — denser at the center, thinning outward.
        for (int i = 0; i < forestTrees; i++)
        {
            (int cx, int cy) = forestCenters[rng.Next(forestCenters.Count)];
            int x = cx + (rng.Next(-forestRadius, forestRadius + 1) + rng.Next(-forestRadius, forestRadius + 1)) / 2;
            int y = cy + (rng.Next(-forestRadius, forestRadius + 1) + rng.Next(-forestRadius, forestRadius + 1)) / 2;
            if (x < 2 || x >= width - 2 || y < 2 || y >= height - 2) continue;
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateTree(x, y);
        }

        // A few scattered trees elsewhere so the rest of the map isn't bare
        for (int i = 0; i < scatteredTrees; i++)
        {
            int x = rng.Next(width / 3, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateTree(x, y);
        }

        // Bushes — uniform scatter across the whole map. Fewer total than before
        // so foraging is not trivial, but no clumping: the rabbits need coverage.
        int bushCount = (width * height) / 40;
        for (int i = 0; i < bushCount; i++)
        {
            int x = rng.Next(2, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateBush(x, y);
        }

        // Spawn rabbits — keep them in one pasture but not stacked. Min distance 2
        // (squared distance >= 4) between any two rabbits at spawn so they don't
        // block each other's BFS paths on tick 0.
        List<(int x, int y)> placedRabbits = new List<(int, int)>();
        int minRabbitDistSq = 4;
        for (int i = 0; i < 8; i++)
        {
            // Accept the cell only if it's creature-spawnable AND far enough from every placed rabbit.
            (int rx, int ry) = World.FindCell((cx, cy) =>
            {
                if (!World.CanCreatureBeHere(cx, cy)) return false;
                foreach ((int px, int py) in placedRabbits)
                {
                    int ddx = cx - px;
                    int ddy = cy - py;
                    if (ddx * ddx + ddy * ddy < minRabbitDistSq) return false;
                }
                return true;
            }, rng);

            if (rx >= 0)
            {
                Archetypes.CreateRabbit(rx, ry);
                placedRabbits.Add((rx, ry));
            }
        }

        // Wolf raids — no persistent wolves. The spawner singleton rolls a small
        // chance each tick of releasing a wolf from a random tree. That wolf hunts
        // one rabbit, then retreats to the forest and vanishes.
        Archetypes.CreateWolfRaidSpawner();
    }
}
