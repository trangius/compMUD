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

        // Scatter trees
        int treeCount = (width * height) / 20;
        for (int i = 0; i < treeCount; i++)
        {
            int x = rng.Next(2, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateTree(x, y);
        }

        // Scatter bushes
        int bushCount = (width * height) / 15;
        for (int i = 0; i < bushCount; i++)
        {
            int x = rng.Next(2, width - 2);
            int y = rng.Next(2, height - 2);
            if (!World.IsOpenGround(x, y)) continue;
            Archetypes.CreateBush(x, y);
        }

        // Spawn rabbits
        for (int i = 0; i < 12; i++)
        {
            (int rx, int ry) = World.FindCell(World.IsCreatureSpawnable, rng);
            if (rx >= 0) Archetypes.CreateRabbit(rx, ry);
        }

        // Spawn wolves
        for (int i = 0; i < 3; i++)
        {
            (int wx, int wy) = World.FindCell(World.IsCreatureSpawnable, rng);
            if (wx >= 0) Archetypes.CreateWolf(wx, wy);
        }
    }
}
