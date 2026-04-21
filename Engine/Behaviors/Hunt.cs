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

    // ----------------------------------------------------------------------------
    // Read the shared per-tick flow field for each prey species this hunter
    // hunts. FlowFieldHelper picks the best-step neighbor across them. If the
    // chosen neighbor's distance is 0, the neighbor IS a prey cell — bite the
    // prey sitting there. Otherwise cache the step and walk next tick.
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

        if (!FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, range, rng, out FlowFieldStep step))
            return false;

        // Distance 0 at the chosen neighbor means it's a prey cell (a seed of
        // the flow field). Pull the prey id — must be a Species entity on our
        // prey list AND have Health, matching the flow-field seeder's filter.
        // If the prey evaporated between tick start and now, fall through to
        // "walk there anyway" (rare).
        if (step.bestDist == 0)
        {
            foreach (int other in World.EntitiesAt(step.neighborX, step.neighborY))
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
        cachedStepDx = step.stepDx;
        cachedStepDy = step.stepDy;
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
            // Capture prey state BEFORE anything destructive — DeathHelper
            // will destroy the entity below, and "attacks ?" in the log
            // comes from Label() falling back to the name-fallback once
            // the entity is gone. Also remember the pinned flag for the
            // "pins" vs "attacks" verb below.
            Position preyPos = World.GetComponent<Position>(cachedPreyId);
            int preyX = preyPos.X, preyY = preyPos.Y;
            bool wasPinned = World.HasComponent<Grappled>(cachedPreyId);

            // Bite: damage owned by the Melee component (attacker Str vs defender Tough)
            int damage = World.GetComponent<Melee>(id).Damage(id, cachedPreyId);
            Health targetHealth = World.GetComponent<Health>(cachedPreyId);
            targetHealth.TakeDamage(damage);

            // Log BEFORE DeathHelper so the names resolve. Order reads as
            // "hunter attacks rabbit (0/10 HP)" then "rabbit dies".
            // "pins" on the first bite that sticks, "attacks" after (or on kill).
            bool willKill = targetHealth.Current <= 0;
            string verb = (willKill || wasPinned) ? "attacks" : "pins";
            World.Log($"{World.Label(id)} {verb} {World.Label(cachedPreyId)} ({targetHealth.Current}/{targetHealth.Max} HP)");

            bool killed = DeathHelper.DestroyEntityIfDead(cachedPreyId);

            // Raider mission complete — flip the flag and ReturnToForest takes over next tick
            if (killed && World.HasComponent<RaidingWolf>(id))
                World.GetComponent<RaidingWolf>(id).hasKilled = true;

            if (killed)
            {
                // Step onto the corpse cell. Being Solid on the kill blocks other
                // predators from poaching, and next tick's Feed underfoot check
                // finds it naturally.
                Position mine = World.GetComponent<Position>(id);
                MovementHelper.TryMove(id, preyX - mine.X, preyY - mine.Y);
            }
            else
            {
                // Pin the survivor so it can't just flee next tick. AttachComponent
                // replaces any existing Grappled — so a second bite refreshes the grip.
                World.AttachComponent(cachedPreyId, new Grappled { attackerId = id });
            }

            return 3;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }
}
