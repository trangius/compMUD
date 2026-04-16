namespace Engine;

// State: what "kind" of thing this entity is. The spawn delegate IS the species
// identity — two entities are the same species iff their Species.spawn points
// at the same archetype method. Compared by reference (pointer equality).
// Used by breeding ("is that my mate?"), vegetation clustering ("is that another
// bush?"), and future predation ("is that on my prey list?").
public class Species
{
    public required Func<int, int, int> spawn;  // archetype's Create method — used as species ID

    // ----------------------------------------------------------------------------
    // Count of every entity in the world whose Species matches speciesSpawn.
    // Use for global caps (e.g. rabbit population ceiling).
    // ----------------------------------------------------------------------------
    public static int CountAll(Func<int, int, int> speciesSpawn)
    {
        int count = 0;
        foreach (int id in World.AllWithComponent<Species>())
        {
            if (World.GetComponent<Species>(id).spawn == speciesSpawn) count++;
        }
        return count;
    }

    // ----------------------------------------------------------------------------
    // Count of same-species entities in a (2*radius+1)x(2*radius+1) square around
    // (centerX, centerY). excludeId is skipped — pass -1 for "don't skip anyone",
    // or the caller's own id to avoid counting themselves. Use for local caps
    // (e.g. bush cluster density).
    // ----------------------------------------------------------------------------
    public static int CountInRadius(int centerX, int centerY, int radius, Func<int, int, int> speciesSpawn, int excludeId = -1)
    {
        int count = 0;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                foreach (int other in World.EntitiesAt(centerX + dx, centerY + dy))
                {
                    if (other == excludeId) continue;
                    if (!World.HasComponent<Species>(other)) continue;
                    if (World.GetComponent<Species>(other).spawn == speciesSpawn) count++;
                }
            }
        }
        return count;
    }
}
