namespace Engine;

// ----------------------------------------------------------------------------
// What each creature and object is made of. Look here to see "what is a rabbit."
// ----------------------------------------------------------------------------
public static class Archetypes
{
    // Shared ecology rng — creature AI noise (wander, flee, feed, breed,
    // grapple escape). Behaviors that take a Random get this one. Per-feature
    // streams whose cadence shouldn't drift as unrelated systems get added
    // (wolf raids, future weather, future quests) own their own Random in
    // their feature file — see WolfRaid.cs for the pattern.
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
        World.AttachComponent(e, new Stats { Strength = 10, Agility = 70, Perception = 15, Toughness = 15 });
        World.AttachComponent(e, new Health(10));
        World.AttachComponent(e, new Energy(3000));
        // When killed, leaves meat (edible), pelt (durable — wolves also eat
        // it, hunters will take it), and bones (durable, inedible to wolves).
        World.AttachComponent(e, new Yields(
            new Yield(Resources.Meat, 500),
            new Yield(Resources.Pelt, 100),
            new Yield(Resources.Bone, 50)
        ));
        World.AttachComponent(e, new Diet(Resources.Berry));
        World.AttachComponent(e, new Species { spawn = CreateRabbit });
        World.AttachComponent(e, new AgilityPaced());
        // Tuned for combined wolf-raid + hunter predation. Below these rates
        // the rabbits crashed to zero under pressure from both directions.
        World.AttachComponent(e, new Breeding { breedCooldown = 250, breedChance = 0.15, globalCap = 30 });
        World.AttachComponent(e, new Behaviors(
            new EscapeGrappleBehavior(rng),
            new RunFromPredatorBehavior(rng),
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
        World.AttachComponent(e, new Stats { Strength = 80, Agility = 75, Perception = 100, Toughness = 50 });
        World.AttachComponent(e, new AgilityPaced());
        World.AttachComponent(e, new Predator(CreateRabbit));
        World.AttachComponent(e, new RaidingWolf());
        World.AttachComponent(e, new Health(30));
        World.AttachComponent(e, new Melee());
        World.AttachComponent(e, new Energy(1000));
        // When killed, same three yields as a rabbit but tuned larger/smaller
        // in different ways — more meat (a whole wolf), pelt, and bones.
        World.AttachComponent(e, new Yields(
            new Yield(Resources.Meat, 350),
            new Yield(Resources.Pelt, 120),
            new Yield(Resources.Bone, 80)
        ));
        // Wolves eat meat AND pelt off a kill — in reality they'd trash the
        // pelt, but here it keeps the bones-leftover mechanic simple and lets
        // the ecology fall out of diet declarations.
        World.AttachComponent(e, new Diet(Resources.Meat, Resources.Pelt) { hungerThreshold = 0.9 });
        World.AttachComponent(e, new Behaviors(
            new ReturnToForestBehavior(rng),
            new HuntBehavior(rng),
            new FeedBehavior(rng),
            new WanderBehavior(rng)
        ));
        World.AttachComponent(e, new Effects(new EnergyDrainEffect()));
        return e;
    }

    // ----------------------------------------------------------------------------
    // A camp: single tile that holds a stockpile. No behaviors — the camp is
    // inert infrastructure. Its Container is what hunters deposit into and
    // eat from. Capacity is big so several kills' worth fit before overflow.
    // ----------------------------------------------------------------------------
    public static int CreateCamp(int x, int y)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "camp", layer = 2 });
        World.AttachComponent(e, new Named { name = "Camp" });
        World.AttachComponent(e, new Walkable());
        World.AttachComponent(e, new Camp());
        World.AttachComponent(e, new Container(capacity: 5000));
        return e;
    }

    // ----------------------------------------------------------------------------
    // A hunter: humanoid tied to a camp. Has the full state profile of an
    // economic actor — backpack, home pointer, diet/collects split, hunter
    // stats — but NO behaviors yet. The hunter stands still in this commit;
    // the six behaviors that make the loop work land next.
    //
    // No Species — hunters don't breed and nothing hunts them yet. Add when
    // a human-targeting predator or a breeding mechanic appears.
    //
    // Strength 60 drops a rabbit in one hit (rabbit HP 10); Toughness 40 is
    // less than a wolf's 50. Agility 72 beats rabbit's 70 so the hunter can
    // actually close on fleeing prey — period 85-Ag = 13 vs rabbit's 15.
    // Revisit if traps or ranged weapons show up; a real human is slower
    // than a rabbit.
    //
    // Energy drains at 0.25 units/tick — a trained human on expedition burns
    // slower than a creature's baseline 1.0. Has to be low enough that
    // meat-in-per-trip outpaces energy-out-per-trip, or the stockpile
    // bleeds and the hunter eventually starves.
    // ----------------------------------------------------------------------------
    public static int CreateHunter(int x, int y, int campId)
    {
        int e = World.CreateEntity();
        World.AttachComponent(e, new Position(x, y));
        World.AttachComponent(e, new Appearance { spriteId = "hunter", layer = 4 });
        World.AttachComponent(e, new Named { name = "Hunter" });
        World.AttachComponent(e, new Solid());
        World.AttachComponent(e, new Stats { Strength = 60, Agility = 72, Perception = 80, Toughness = 40 });
        World.AttachComponent(e, new AgilityPaced());
        World.AttachComponent(e, new Health(50));
        World.AttachComponent(e, new Melee());
        World.AttachComponent(e, new Energy(3000, drainPerTick: 0.25));
        World.AttachComponent(e, new Predator(CreateRabbit));
        // Diet gates eating; Collects gates what goes in the backpack.
        // Hunter eats meat but hauls pelts home as cargo.
        World.AttachComponent(e, new Diet(Resources.Meat));
        World.AttachComponent(e, new Collects(Resources.Meat, Resources.Pelt));
        // Backpack — sized to comfortably hold three rabbits' meat + pelt
        // per trip. Drop below that and the hunter leaves cargo on the last
        // kill of every outing.
        World.AttachComponent(e, new Container(capacity: 2000));
        World.AttachComponent(e, new Home(campId));
        // Placeholder yields for if a hunter is killed.
        World.AttachComponent(e, new Yields(
            new Yield(Resources.Meat, 400),
            new Yield(Resources.Bone, 60)
        ));
        // The hunter's loop: eat, deposit, butcher, hunt, walk home. Priorities
        // in descending order so the most immediate needs beat the rest.
        // Escape-grapple at 100 still preempts everything — a pinned hunter
        // struggles free first.
        World.AttachComponent(e, new Behaviors(
            new EscapeGrappleBehavior(rng),
            new FeedFromCampBehavior(),
            new FeedFromBackpackBehavior(),
            new ButcherCorpseBehavior(rng),
            new DepositAtCampBehavior(),
            new HuntForStockpileBehavior(rng),
            new ReturnToCampBehavior(rng)
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
        World.AttachComponent(e, new Effects(new WolfRaidEffect()));
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
        World.AttachComponent(e, new FixedPaced { period = 30 });
        World.AttachComponent(e, new Vegetation { spreadChance = 0.03, spawnChance = 0.0005, clusterCap = 2, clusterRadius = 2 });
        // A grazed bush — rabbits drain the berry yield directly, no item
        // pops out. When empty, FeedBehavior destroys the bush.
        World.AttachComponent(e, new Yields(new Yield(Resources.Berry, 1500)));
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
