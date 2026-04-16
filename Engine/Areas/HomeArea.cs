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

        // Trees — a dense forest in the NW corner (wolves emerge from and retreat
        // to these cells), plus a few scattered trees across the rest of the map.
        int treeCount = (width * height) / 20;
        int forestTrees = treeCount * 2 / 3;
        int scatteredTrees = treeCount - forestTrees;

        // Dense cluster in NW corner — high chance of trees inside this box
        int forestMaxX = width / 3;
        int forestMaxY = height * 2 / 3;
        for (int i = 0; i < forestTrees; i++)
        {
            int x = rng.Next(2, forestMaxX);
            int y = rng.Next(2, forestMaxY);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateTree(x, y);
        }

        // A few scattered trees elsewhere so the map isn't a featureless meadow
        for (int i = 0; i < scatteredTrees; i++)
        {
            int x = rng.Next(forestMaxX, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateTree(x, y);
        }

        // Scatter bushes
        int bushCount = (width * height) / 25;
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
        for (int i = 0; i < 12; i++)
        {
            // Accept the cell only if it's creature-spawnable AND far enough from every placed rabbit.
            (int rx, int ry) = World.FindCell((cx, cy) =>
            {
                if (!World.IsCreatureSpawnable(cx, cy)) return false;
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
