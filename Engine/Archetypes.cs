namespace Engine;

// ----------------------------------------------------------------------------
// What each creature and object is made of. Look here to see "what is a rabbit."
// ----------------------------------------------------------------------------
public static class Archetypes
{
    private static Random rng = new Random(42);

    // ----------------------------------------------------------------------------
    // A rabbit: flees predators, eats bushes, breeds, wanders.
    // ----------------------------------------------------------------------------
    public static int CreateRabbit(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "rabbit", layer = 4 });
        World.AttachComponent(e, new Named { name = "Rabbit" });
        World.AttachComponent(e, new Solid());
        World.AttachComponent(e, new Sensing(15));
        World.AttachComponent(e, new Health(10));
        World.AttachComponent(e, new Energy(3000));
        World.AttachComponent(e, new Drops { name = "Rabbit corpse", resourceType = Resources.Meat, amount = 600, dropSpriteId = "corpse" });
        World.AttachComponent(e, new Diet(Resources.Berry));
        World.AttachComponent(e, new Species { spawn = CreateRabbit });
        World.AttachComponent(e, new Scheduler { period = 15 });
        World.AttachComponent(e, new Breeding { breedCooldown = 400, breedChance = 0.1, globalCap = 12 });
        World.AttachComponent(e, new Behaviors(
            new FleeBehavior(),
            new HarvestBehavior(),
            new FeedBehavior(rng),
            new BreedBehavior(rng),
            new RestBehavior(),
            new WanderBehavior(rng)
        ));
        World.AttachComponent(e, new Effects(new EnergyDrainEffect()));
        return e;
    }

    // ----------------------------------------------------------------------------
    // A wolf: hunts prey, eats, wanders.
    // ----------------------------------------------------------------------------
    public static int CreateWolf(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "wolf", layer = 4 });
        World.AttachComponent(e, new Named { name = "Wolf" });
        World.AttachComponent(e, new Solid());
        World.AttachComponent(e, new Species { spawn = CreateWolf });
        World.AttachComponent(e, new Scheduler { period = 10 });
        World.AttachComponent(e, new Predator(CreateRabbit));
        World.AttachComponent(e, new RaidingWolf());
        World.AttachComponent(e, new Sensing(100));
        World.AttachComponent(e, new Health(30));
        World.AttachComponent(e, new Attacking(8));
        World.AttachComponent(e, new Energy(1000));
        World.AttachComponent(e, new Drops { name = "Wolf corpse", resourceType = Resources.Meat, amount = 400, dropSpriteId = "corpse" });
        World.AttachComponent(e, new Diet(Resources.Meat) { hungerThreshold = 0.9 });
        World.AttachComponent(e, new Behaviors(
            new ReturnToForestBehavior(),
            new HuntBehavior(),
            new HarvestBehavior(),
            new FeedBehavior(rng),
            new WanderBehavior(rng)
        ));
        World.AttachComponent(e, new Effects(new EnergyDrainEffect()));
        return e;
    }

    // ----------------------------------------------------------------------------
    // Wolf raid spawner: a singleton entity with no position, just an Effect that
    // rolls a per-tick chance of unleashing a wolf from a random tree cell.
    // Keeps area-level event logic declarative — HomeArea just creates one.
    // ----------------------------------------------------------------------------
    public static int CreateWolfRaidSpawner()
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Named { name = "Wolf raid spawner" });
        World.AttachComponent(e, new Effects(new WolfRaidEffect(rng)));
        return e;
    }

    // ----------------------------------------------------------------------------
    // A bush: food source that spreads. Drop berries when harvested.
    // ----------------------------------------------------------------------------
    public static int CreateBush(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "bush", layer = 2 });
        World.AttachComponent(e, new Named { name = "Bush" });
        World.AttachComponent(e, new Walkable());
        World.AttachComponent(e, new Species { spawn = CreateBush });
        World.AttachComponent(e, new Scheduler { period = 30 });
        World.AttachComponent(e, new Vegetation { spreadChance = 0.1, spawnChance = 0.0005, localCap = 2, localRadius = 2 });
        World.AttachComponent(e, new Drops { name = "Berries", resourceType = Resources.Berry, amount = 1500, dropSpriteId = "berries" });
        World.AttachComponent(e, new Behaviors(new GrowBehavior(rng)));
        return e;
    }

    // ----------------------------------------------------------------------------
    // A tree: inert scenery. Trees don't regrow on any game-reasonable timescale,
    // so they have no Vegetation or Behaviors — just a passable tile that renders.
    // The Tree marker lets raiding wolves find forest cells to spawn at / retreat to.
    // ----------------------------------------------------------------------------
    public static int CreateTree(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "tree", layer = 2 });
        World.AttachComponent(e, new Walkable());
        World.AttachComponent(e, new Tree());
        return e;
    }

    // ----------------------------------------------------------------------------
    // Grass: the default walkable ground tile.
    // ----------------------------------------------------------------------------
    public static int CreateGrass(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "grass", layer = 0 });
        World.AttachComponent(e, new Walkable());
        return e;
    }

    // ----------------------------------------------------------------------------
    // Wall: blocks movement (no Walkable — nothing can step onto it).
    // ----------------------------------------------------------------------------
    public static int CreateWall(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "wall", layer = 0 });
        return e;
    }

    // ----------------------------------------------------------------------------
    // Water: blocks walking creatures (no Walkable). Swimmers come later.
    // ----------------------------------------------------------------------------
    public static int CreateWater(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "water", layer = 0 });
        return e;
    }
}
