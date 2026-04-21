namespace Engine;

// State: this entity hunts specific species. `preySpecies` is a set of archetype
// spawn delegates — the same pointers used as Species identity. A wolf with
// preySpecies = { CreateRabbit } will chase rabbits (and only rabbits). Set membership
// replaces the old role marker, the same way Diet.Accepts checks resource kinds.
public class Predator
{
    public HashSet<Func<int, int, int>> preySpecies;

    public Predator(params Func<int, int, int>[] prey)
    {
        preySpecies = new HashSet<Func<int, int, int>>(prey);
    }

    // ----------------------------------------------------------------------------
    // Does my prey list include this species?
    // ----------------------------------------------------------------------------
    public bool Hunts(Func<int, int, int> speciesSpawn)
    {
        return preySpecies.Contains(speciesSpawn);
    }
}

// State: this entity can make melee attacks. Owns the damage formula — the
// component answers "how hard does a strike hit" by reading attacker Strength
// and defender Toughness off the two entities. A future Weapon component
// would compose here: `Strength + weapon.bonus`.
public class Melee
{
    // ----------------------------------------------------------------------------
    // Damage this entity deals to a defender on a successful melee strike.
    // Attacker Strength scores the hit; defender Toughness soaks part of it.
    // Floored at 1 so any bite lands something. Scale: Str 1-100 → base 1-4;
    // Toughness 1-100 → soak 0-3. Wolf (Str 80) vs Rabbit (Tough 15) = 3;
    // Rabbit (Str 10) vs Wolf (Tough 50) = 1 (floor).
    // ----------------------------------------------------------------------------
    public int Damage(int attackerId, int defenderId)
    {
        int atk = StatMath.Require(attackerId).Strength / 25;
        int soak = StatMath.Require(defenderId).Toughness / 30;
        return Math.Max(1, atk - soak);
    }
}

// Behavior: chase the nearest REACHABLE prey. Same BFS-first pattern as Feed —
// flood the walkable cells once, then scan the flood for prey and pick the one
// whose nearest approach cell has the smallest BFS distance. No Euclidean
// vision circle: "visible" and "reachable" are the same concept here, so wolves
// never lock onto prey that's technically in sight but walled off.
//
// Prey's own cell is Solid so BFS can't enter it — we target the nearest of
// the prey's 8 neighbors and walk there. If one of those neighbors is the
// wolf's own cell (distance 0), the wolf is already adjacent and bites.
//
// Priority sits BELOW Feed on purpose: a hungry wolf with a corpse underfoot
// should eat it before chasing a live rabbit across the map. Feed returns
// false when not hungry or when no food is reachable, so Hunt still runs in
// the normal case — it only yields when there's food already within reach.
public class HuntBehavior : IBehavior
{
    public int Priority => 20;

    // Random tie-breaks when several neighbor cells point at equally-close
    // prey — without this, a wolf pack converging on the same rabbit funnels
    // through one cell and stacks up.
    private Random rng;

    public HuntBehavior(Random rng)
    {
        this.rng = rng;
    }

    // Cached between WouldAct and Act.
    private int cachedPreyId = -1;
    private bool cachedPreyAdjacent;
    private int cachedStepDx;
    private int cachedStepDy;

    // 8-connected neighborhood — the wolf's own cell is Solid so the flow
    // field never contains it; instead the wolf reads these 8 cells to find
    // the one that's closest to prey.
    private static readonly (int dx, int dy)[] neighborOffsets = {
        (0, -1), (0, 1), (1, 0), (-1, 0),
        (1, -1), (1, 1), (-1, -1), (-1, 1)
    };

    // ----------------------------------------------------------------------------
    // Read the shared per-tick flow field for each prey species this hunter
    // hunts. Walk the hunter's 8 neighbors, pick the one with smallest field
    // distance across all prey species (random tiebreak). That neighbor is the
    // next step. If its distance is 0, the neighbor IS a prey cell — bite the
    // prey sitting there. If the step-effective distance (d + 1, one for the
    // hunter's own step) exceeds vision, decline.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Melee>(id) || !World.HasComponent<Predator>(id) || !World.HasComponent<Position>(id)) return false;

        Predator predator = World.GetComponent<Predator>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = StatMath.VisionRange(id);

        // Union the flow fields of every species on our prey list. In practice
        // most predators hunt a single species, so this loop is short.
        List<FlowField> fields = new List<FlowField>();
        foreach (Func<int, int, int> preySpawn in predator.preySpecies)
            fields.Add(World.GetSpeciesFlowField(preySpawn));

        // Scan our 8 neighbors against every prey field; pick the smallest
        // distance, collect ties for random pick.
        int bestDist = int.MaxValue;
        List<(int nx, int ny, int dx, int dy)> tied = new List<(int, int, int, int)>();
        foreach ((int dx, int dy) offset in neighborOffsets)
        {
            int nx = pos.X + offset.dx;
            int ny = pos.Y + offset.dy;

            // Each prey field might give this cell a different distance; take
            // the minimum since we want the nearest prey of ANY hunted species.
            int cellBest = int.MaxValue;
            foreach (FlowField f in fields)
            {
                if (!f.Reachable(nx, ny)) continue;
                int d = f.Distance(nx, ny);
                if (d < cellBest) cellBest = d;
            }
            if (cellBest == int.MaxValue) continue;  // no prey reachable via this neighbor

            if (cellBest < bestDist)
            {
                bestDist = cellBest;
                tied.Clear();
                tied.Add((nx, ny, offset.dx, offset.dy));
            }
            else if (cellBest == bestDist)
            {
                tied.Add((nx, ny, offset.dx, offset.dy));
            }
        }

        if (tied.Count == 0) return false;  // no reachable prey in any direction

        // Hunter's effective distance is neighbor-dist + 1 (we still have to
        // step to the neighbor). Out-of-vision prey — ignore.
        if (bestDist + 1 > range) return false;

        (int pickNx, int pickNy, int stepDx, int stepDy) = tied[rng.Next(tied.Count)];

        // Distance 0 at a neighbor means the neighbor IS a prey cell (seed of
        // the flow field). Pull the prey id from that cell — it must be a
        // Species entity on our prey list AND have Health, same filter the
        // flow-field seeder applied. If the prey evaporated between tick
        // start and now, fall through to "walk there anyway" (rare).
        if (bestDist == 0)
        {
            foreach (int other in World.EntitiesAt(pickNx, pickNy))
            {
                if (other == id) continue;
                if (!World.HasComponent<Species>(other)) continue;
                if (!predator.Hunts(World.GetComponent<Species>(other).spawn)) continue;
                if (!World.HasComponent<Health>(other)) continue;
                cachedPreyId = other;
                cachedPreyAdjacent = true;
                return true;
            }
        }

        cachedPreyAdjacent = false;
        cachedStepDx = stepDx;
        cachedStepDy = stepDy;
        return true;
    }

    // ----------------------------------------------------------------------------
    // Adjacent case: bite (cost 3 — the predator commits to an attack, takes
    // longer than a step). Walking case: step along the cached BFS path (cost 1).
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (cachedPreyAdjacent)
        {
            // Bite: damage owned by the Melee component (attacker Str vs defender Tough)
            int damage = World.GetComponent<Melee>(id).Damage(id, cachedPreyId);
            Health targetHealth = World.GetComponent<Health>(cachedPreyId);
            targetHealth.TakeDamage(damage);

            World.Log($"{World.Label(id)} attacks {World.Label(cachedPreyId)} ({targetHealth.Current}/{targetHealth.Max} HP)");
            bool killed = DeathHelper.DestroyEntityIfDead(cachedPreyId);

            // Raider mission complete — flip the flag and ReturnToForest takes over next tick
            if (killed && World.HasComponent<RaidingWolf>(id))
                World.GetComponent<RaidingWolf>(id).hasKilled = true;

            // Pin the survivor so it can't just flee next tick. AttachComponent
            // replaces any existing Grappled — so a second bite refreshes the grip.
            if (!killed)
                World.AttachComponent(cachedPreyId, new Grappled { attackerId = id });

            return 3;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }
}
