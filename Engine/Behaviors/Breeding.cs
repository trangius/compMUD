namespace Engine;

// State: this entity can breed. Species identity lives on the separate Species
// component; Breeding only carries the reproductive schedule + population cap.
// globalCap sets a map-wide ceiling for the species — once reached, breeding stalls.
public class Breeding
{
    public int breedCooldown = 250;
    public double breedChance = 0.008;
    public int lastBreedTick = -250;
    public int globalCap = int.MaxValue;  // max same-species entities allowed in the world
}

// Behavior: seek a mate and reproduce.
public class BreedBehavior : IBehavior
{
    public int Priority => 20;

    private const double minEnergyToBreed = 0.5;

    private Random rng;

    // Cached between WouldAct and Act — who we decided to approach or mate with.
    private int cachedMateId = -1;
    private bool cachedMateIsAdjacent;

    // Where we look for an adjacent mate: 8-connected neighbors (diagonals too).
    // Matches 8-connected movement — a rabbit that stepped diagonally next to a
    // potential mate shouldn't have to shuffle cardinally first to mate.
    private static readonly (int dx, int dy)[] adjacentOffsets = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

    public BreedBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Ready to breed (cooldown OK, fed enough, below population cap) AND a mate
    // is reachable. If mate is adjacent, roll the breed-chance now — that roll
    // IS the decision. A failed adjacent roll returns false, letting another
    // behavior run instead.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Breeding>(id) || !World.HasComponent<Species>(id) || !World.HasComponent<Position>(id)) return false;

        Breeding breeding = World.GetComponent<Breeding>(id);
        Species species = World.GetComponent<Species>(id);

        // Cooldown gate
        if (World.tickCount - breeding.lastBreedTick < breeding.breedCooldown) return false;

        // Energy gate — too hungry to breed
        if (World.HasComponent<Energy>(id))
        {
            Energy energy = World.GetComponent<Energy>(id);
            if (energy.Current <= energy.Max * minEnergyToBreed) return false;
        }

        // Global population gate — species at its map-wide cap doesn't reproduce.
        // Positive check: "is there room for one more of me?" rather than blocking.
        if (breeding.globalCap < int.MaxValue && Species.CountAll(species.spawn) >= breeding.globalCap)
            return false;

        Position pos = World.GetComponent<Position>(id);

        // Adjacent mate? Roll — if the roll fails, yield this tick to another behavior
        int adjacentMate = FindAdjacentMate(id, pos, breeding, species);
        if (adjacentMate >= 0)
        {
            if (rng.NextDouble() < breeding.breedChance)
            {
                cachedMateId = adjacentMate;
                cachedMateIsAdjacent = true;
                return true;
            }
            return false;
        }

        // No adjacent mate — look for one to walk toward
        if (!World.HasComponent<Sensing>(id)) return false;
        int range = World.GetComponent<Sensing>(id).VisionRange;

        cachedMateId = World.FindNearestEntity(pos.X, pos.Y, range, other =>
            other != id
            && World.HasComponent<Species>(other)
            && World.GetComponent<Species>(other).spawn == species.spawn
            && World.HasComponent<Breeding>(other));

        if (cachedMateId < 0) return false;

        cachedMateIsAdjacent = false;
        return true;
    }

    // ----------------------------------------------------------------------------
    // If the cached mate is adjacent, mate and spawn a baby. Otherwise walk toward.
    // Both parents go on cooldown after mating.
    // ----------------------------------------------------------------------------
    public void Act(int id)
    {
        Position pos = World.GetComponent<Position>(id);

        if (cachedMateIsAdjacent)
        {
            Breeding breeding = World.GetComponent<Breeding>(id);
            Species species = World.GetComponent<Species>(id);
            breeding.lastBreedTick = World.tickCount;
            World.GetComponent<Breeding>(cachedMateId).lastBreedTick = World.tickCount;

            // Baby is freshly built from the archetype — full HP and energy by construction.
            // Gotcha: baby spawns at the parent's cell, so two Solids briefly overlap.
            // TryMove gates entry, not placement, so each steps to an empty neighbor on later ticks.
            int baby = species.spawn(pos.X, pos.Y);
            World.GetComponent<Breeding>(baby).lastBreedTick = World.tickCount;  // born on cooldown

            World.Log($"{World.GetEntityName(id)} born at ({pos.X},{pos.Y})");
            return;
        }

        // Mate exists but isn't adjacent — walk toward
        Position matePos = World.GetComponent<Position>(cachedMateId);
        MovementHelper.MoveToward(id, pos, matePos.X, matePos.Y);
    }

    // ----------------------------------------------------------------------------
    // Check the 8 neighbors for a same-species mate off cooldown.
    // ----------------------------------------------------------------------------
    private int FindAdjacentMate(int id, Position pos, Breeding breeding, Species species)
    {
        foreach ((int dx, int dy) offset in adjacentOffsets)
        {
            foreach (int other in World.EntitiesAt(pos.X + offset.dx, pos.Y + offset.dy))
            {
                if (other == id) continue;
                if (!World.HasComponent<Species>(other)) continue;
                if (World.GetComponent<Species>(other).spawn != species.spawn) continue;
                if (!World.HasComponent<Breeding>(other)) continue;

                Breeding otherBreeding = World.GetComponent<Breeding>(other);
                if (World.tickCount - otherBreeding.lastBreedTick < breeding.breedCooldown) continue;

                return other;
            }
        }
        return -1;
    }
}
