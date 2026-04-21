namespace Engine;

// State: this entity belongs to a camp. The id points at the camp entity
// the hunter returns to between trips. Pure data, but it's the whole reason
// the hunter has a goal loop at all — without a home there's no "back to
// base" concept.
public class Home
{
    public int campId;

    public Home(int campId)
    {
        this.campId = campId;
    }
}

// State: this entity is a camp. Single-tile for now. The marker lets
// anything that cares about "the camp" find it — hunters returning home,
// future raiders, the player, rendering. Storage is a Container on the
// same entity.
public class Camp { }

// ----------------------------------------------------------------------------
// Shared helpers for hunter behaviors. Both camp-at checks and the camp's
// storage lookup go through here so the "at camp" question has one answer.
// ----------------------------------------------------------------------------
internal static class HunterHelpers
{
    // Hunter is standing on its camp's cell.
    public static bool AtCamp(int id)
    {
        if (!World.HasComponent<Home>(id) || !World.HasComponent<Position>(id)) return false;
        int campId = World.GetComponent<Home>(id).campId;
        if (!World.EntityExists(campId) || !World.HasComponent<Position>(campId)) return false;
        Position my = World.GetComponent<Position>(id);
        Position camp = World.GetComponent<Position>(campId);
        return my.X == camp.X && my.Y == camp.Y;
    }

    // The Container on this hunter's camp. Null if the camp's gone or unwired.
    public static Container? CampStorage(int id)
    {
        if (!World.HasComponent<Home>(id)) return null;
        int campId = World.GetComponent<Home>(id).campId;
        if (!World.EntityExists(campId) || !World.HasComponent<Container>(campId)) return null;
        return World.GetComponent<Container>(campId);
    }
}

// ----------------------------------------------------------------------------
// Behavior: at camp, hungry, food in storage — eat.
//
// Drains meat from the camp container into the hunter's Energy. Priority
// sits above everything except escape-grapple and flee — a hunter at home
// with food should eat before picking up any other task.
// ----------------------------------------------------------------------------
public class FeedFromCampBehavior : IBehavior
{
    public int Priority => 50;

    // ----------------------------------------------------------------------------
    // At camp, hungry, and the camp has something the hunter eats?
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Home>(id) || !World.HasComponent<Diet>(id) || !World.HasComponent<Energy>(id)) return false;
        if (!HunterHelpers.AtCamp(id)) return false;

        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        if (!diet.IsHungry(energy)) return false;

        Container? storage = HunterHelpers.CampStorage(id);
        if (storage == null) return false;
        foreach (ItemStack s in storage.stacks)
            if (diet.Accepts(s.category)) return true;
        return false;
    }

    // ----------------------------------------------------------------------------
    // Eat until full or until the stockpile runs out — one action, one full
    // meal. Walks every edible stack, taking just enough from each to top off
    // Energy. No portion cap: the hunter's meal is sized by appetite. Cost 5
    // periods — same duration as any other eating action.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        Container storage = HunterHelpers.CampStorage(id)!;

        int total = 0;
        foreach (ItemStack s in storage.stacks.ToArray())
        {
            if (!diet.Accepts(s.category)) continue;
            int stillNeeded = energy.Max - energy.Current - total;
            if (stillNeeded <= 0) break;
            int taken = storage.Take(s.category, stillNeeded);
            total += taken;
        }
        energy.Restore(total);

        if (total > 0)
            World.Log($"{World.Label(id)} eats {total} at camp");
        return 5;
    }
}

// ----------------------------------------------------------------------------
// Behavior: starving AND carrying food — eat from the backpack. Fallback so
// a hunter cut off from camp (long chase, corpses snatched by wolves) doesn't
// starve while carrying food it could consume. Fires only at critical hunger
// so the hunter still prefers to deliver meat home when possible — the
// stockpile matters more than personal comfort.
// ----------------------------------------------------------------------------
public class FeedFromBackpackBehavior : IBehavior
{
    public int Priority => 45;

    // Fraction of Max Energy below which the hunter will eat from its own
    // pack rather than push on home. Well below the normal hunger threshold
    // so the hunter only consumes its cargo in real emergencies.
    private const double starvingFraction = 0.2;

    // How much the hunter takes in one emergency bite. Unlike FeedFromCamp
    // (which fills to max because the stockpile is plentiful), eating from
    // the pack should preserve as much cargo as possible for camp delivery.
    private const int emergencyBite = 400;

    // ----------------------------------------------------------------------------
    // Starving and carrying any edible stack?
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Energy>(id) || !World.HasComponent<Container>(id) || !World.HasComponent<Diet>(id)) return false;
        Energy energy = World.GetComponent<Energy>(id);
        if (energy.Current >= energy.Max * starvingFraction) return false;
        Diet diet = World.GetComponent<Diet>(id);
        foreach (ItemStack s in World.GetComponent<Container>(id).stacks)
            if (diet.Accepts(s.category)) return true;
        return false;
    }

    // ----------------------------------------------------------------------------
    // Take one bite from the pack. Just enough to climb out of the starving
    // range, not a full meal. Cost 5 — same slow eating.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Diet diet = World.GetComponent<Diet>(id);
        Energy energy = World.GetComponent<Energy>(id);
        Container backpack = World.GetComponent<Container>(id);

        foreach (ItemStack s in backpack.stacks.ToArray())
        {
            if (!diet.Accepts(s.category)) continue;
            int taken = backpack.Take(s.category, emergencyBite);
            energy.Restore(taken);
            if (taken > 0)
                World.Log($"{World.Label(id)} snacks {taken} {s.category.name} from pack");
            break;
        }
        return 5;
    }
}

// ----------------------------------------------------------------------------
// Behavior: a corpse with collectable yields is at or near the hunter —
// butcher it, filling the backpack. Underfoot acts in place; not underfoot
// attaches a NavigatePursuit to the corpse and steps there next tick.
//
// Same priority tier as Butcher in the stash (40) — beats the hunt, loses
// to eating. Self-gates when a pursuit is already running so the hunter
// doesn't thrash between corpses mid-walk. When a corpse is underfoot,
// that gate is bypassed — always pick up what's literally at your feet.
// ----------------------------------------------------------------------------
public class ButcherCorpseBehavior : IBehavior
{
    public int Priority => 40;

    private Random rng;

    // Cached between WouldAct and Act.
    private int cachedCorpseId = -1;
    private bool cachedUnderfoot;
    private int cachedWalkToX;
    private int cachedWalkToY;

    public ButcherCorpseBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Is there a corpse whose yields this hunter Collects AND backpack has
    // room? Underfoot wins instantly. Otherwise, if no pursuit is already
    // running, find the nearest matching corpse via the yield flow fields
    // and commit to walking there.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Position>(id) || !World.HasComponent<Collects>(id) || !World.HasComponent<Container>(id)) return false;

        Container backpack = World.GetComponent<Container>(id);
        if (backpack.Free <= 0) return false;
        Collects collects = World.GetComponent<Collects>(id);
        Position pos = World.GetComponent<Position>(id);

        // Underfoot corpse wins even during a pursuit — act in place is free.
        foreach (int other in World.EntitiesAt(pos.X, pos.Y))
        {
            if (IsButcherTarget(other, id, collects))
            {
                cachedCorpseId = other;
                cachedUnderfoot = true;
                return true;
            }
        }

        // Don't set a new walk-pursuit while one's already running. Conservative
        // rule — prevents "chase every corpse in sight" chains. A corpse we
        // pass close enough to land underfoot still gets butchered (above).
        if (World.HasComponent<Pursuit>(id)) return false;

        // Perception: union of yield flow fields for every category this
        // hunter Collects. The seeder filters to corpses with that category
        // already, so reachability = "a corpse I'd butcher is at that cell".
        int range = StatMath.VisionRange(id);
        List<FlowField> fields = new List<FlowField>();
        foreach (ResourceCategory cat in collects.allowed)
            fields.Add(World.GetYieldFlowField(cat));

        if (!FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, range, rng, out FlowFieldStep step))
            return false;

        // Walk the stepToward chain from the chosen neighbor to the seed cell
        // — that's the corpse's position. Bounded by `step.bestDist` so even
        // at max range this walks fewer than vision iterations.
        int cx = step.neighborX, cy = step.neighborY;
        FlowField? tracker = null;
        foreach (FlowField f in fields)
        {
            if (f.Reachable(cx, cy) && f.Distance(cx, cy) == step.bestDist) { tracker = f; break; }
        }
        if (tracker == null) return false;
        while (tracker.Distance(cx, cy) > 0)
        {
            (int dx, int dy) = tracker.StepToward(cx, cy);
            cx += dx; cy += dy;
        }

        cachedUnderfoot = false;
        cachedWalkToX = cx;
        cachedWalkToY = cy;
        return true;
    }

    // ----------------------------------------------------------------------------
    // Underfoot: drain every collected yield from the corpse into the backpack,
    // capped by backpack free space. Flip to the bones sprite once meat is
    // gone (mirrors Feed's sprite change). Cost 3 — butchering takes real
    // work, more than a bite. Not underfoot: attach a NavigatePursuit to the
    // corpse's cell; cost 0 so the pursuit takes its first step this same tick.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (!cachedUnderfoot)
        {
            // Priority 3 matches the "casual detour" tier — anything important
            // (eating, a nearby rabbit that needs striking) still preempts.
            World.AttachComponent(id, new Pursuit(new NavigatePursuit(cachedWalkToX, cachedWalkToY, priority: 3, rng)));
            World.Log($"{World.Label(id)} heads to butcher a corpse at ({cachedWalkToX},{cachedWalkToY})");
            return 0;
        }

        Collects collects = World.GetComponent<Collects>(id);
        Container backpack = World.GetComponent<Container>(id);
        Yields yields = World.GetComponent<Yields>(cachedCorpseId);

        // Snapshot categories — can't iterate entries while Drain mutates them.
        List<ResourceCategory> targets = new List<ResourceCategory>();
        foreach (Yield y in yields.entries)
            if (collects.Accepts(y.category)) targets.Add(y.category);

        // Drain the smaller piles first. If the pack has 300 free and a
        // corpse yields 500 meat + 100 pelt, taking meat first fills the
        // pack and leaves all the pelt behind. Taking pelt first (100 fits
        // fully) then meat (200 in the remainder) gets a fair mix.
        targets.Sort((a, b) => (yields.Get(a)?.amount ?? 0).CompareTo(yields.Get(b)?.amount ?? 0));

        // Track per-category so the log line can narrate what actually went in
        // the pack ("+300 meat, +100 pelt") instead of a lump total.
        List<(ResourceCategory cat, int amount)> moved = new List<(ResourceCategory, int)>();
        foreach (ResourceCategory cat in targets)
        {
            int want = yields.Get(cat)?.amount ?? 0;
            int canFit = backpack.Free;
            int took = yields.Drain(cat, Math.Min(want, canFit));
            backpack.Add(cat, took);
            if (took > 0) moved.Add((cat, took));
            if (backpack.Free <= 0) break;
        }

        string breakdown = moved.Count == 0
            ? "nothing fit"
            : string.Join(", ", moved.Select(m => $"+{m.amount} {m.cat.name}"));
        World.Log($"{World.Label(id)} butchers {World.Label(cachedCorpseId)} ({breakdown})");

        // Same corpse-to-bones transition Feed triggers when meat runs out.
        if (yields.Get(Resources.Meat) == null && World.HasComponent<Appearance>(cachedCorpseId))
        {
            World.GetComponent<Appearance>(cachedCorpseId).spriteId = "bones";
            if (World.HasComponent<Named>(cachedCorpseId))
            {
                Named n = World.GetComponent<Named>(cachedCorpseId);
                n.name = n.name.Replace("corpse", "bones");
            }
        }
        return 3;
    }

    // Is this entity a corpse with at least one yield the hunter Collects?
    private static bool IsButcherTarget(int other, int butcherId, Collects collects)
    {
        if (other == butcherId) return false;
        if (!World.HasComponent<Corpse>(other)) return false;
        if (!World.HasComponent<Yields>(other)) return false;
        foreach (Yield entry in World.GetComponent<Yields>(other).entries)
            if (collects.Accepts(entry.category)) return true;
        return false;
    }
}

// ----------------------------------------------------------------------------
// Behavior: at camp with something in the backpack — dump it into storage.
//
// Moves every stack from the backpack to the camp's Container, capped by
// storage free space. If the stockpile can't fit everything the hunter keeps
// carrying the excess; simpler than overflow handling.
// ----------------------------------------------------------------------------
public class DepositAtCampBehavior : IBehavior
{
    public int Priority => 35;

    // ----------------------------------------------------------------------------
    // At camp, carrying something, and storage has any room at all?
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!HunterHelpers.AtCamp(id)) return false;
        if (!World.HasComponent<Container>(id)) return false;

        Container backpack = World.GetComponent<Container>(id);
        if (backpack.stacks.Count == 0) return false;

        Container? storage = HunterHelpers.CampStorage(id);
        return storage != null && storage.Free > 0;
    }

    // ----------------------------------------------------------------------------
    // Move stacks from pack to storage until one fills or the other empties.
    // Cost 1 — unloading at camp is quick.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        Container backpack = World.GetComponent<Container>(id);
        Container storage = HunterHelpers.CampStorage(id)!;

        // Snapshot stacks by category — can't iterate while mutating.
        List<(ResourceCategory cat, int amount)> toMove = new List<(ResourceCategory, int)>();
        foreach (ItemStack s in backpack.stacks) toMove.Add((s.category, s.amount));

        int total = 0;
        foreach ((ResourceCategory cat, int amount) in toMove)
        {
            if (storage.Free <= 0) break;
            int moved = Math.Min(amount, storage.Free);
            int taken = backpack.Take(cat, moved);
            storage.Add(cat, taken);
            total += taken;
        }

        World.Log($"{World.Label(id)} deposits {total} at camp");
        return 1;
    }
}

// ----------------------------------------------------------------------------
// Behavior: the camp's stockpile isn't full — go out and kill a rabbit for
// meat. Same hunt mechanic as HuntBehavior on wolves (find nearest prey via
// species flow field, step or strike); the difference is the gating and the
// dynamic priority.
//
// Dynamic priority by stockpile fill:
//   fill > 70%: priority 15  — casual; loses to eating, resting, deliberate idle.
//   50 – 70%: priority 25    — routine work; wins over idle.
//   25 – 50%: priority 50    — stockpile running low; beats eating.
//   < 25%:   priority 80    — emergency; only reflexes like flee/escape interrupt.
// ----------------------------------------------------------------------------
public class HuntForStockpileBehavior : IBehavior
{
    // Thresholds: stockpile fill fractions where the urgency tier shifts.
    // Each tier is the hunter's opinion of how bad things are. Names over
    // numbers — comments explain why the tiers exist, not the exact values.
    private const double comfortableFill = 0.7;
    private const double routineFill = 0.5;
    private const double lowFill = 0.25;

    // Matching priority per tier. All four tiers sit below Butcher (40) on
    // purpose — a hunter with a kill nearby should butcher before chasing the
    // next rabbit, even when the stockpile is critical. The tiers still spread
    // so low-priority filler (Wander, Rest, ReturnToCamp) yields when the
    // stockpile gets dire.
    private const int casualPriority = 15;
    private const int routinePriority = 20;
    private const int urgentPriority = 25;
    private const int criticalPriority = 34;

    // Energy floor: the hunter won't start or continue a hunt below this
    // fraction of Max — too tired. Lets ReturnToCamp take over so the
    // hunter walks home to eat instead of chasing one more rabbit into
    // starvation.
    private const double tooTiredFraction = 0.3;

    private Random rng;

    // Cached between WouldAct and Act.
    private int cachedPreyId = -1;
    private bool cachedPreyAdjacent;
    private int cachedStepDx;
    private int cachedStepDy;

    // Priority is computed in WouldAct (based on current stockpile state)
    // and read by the dispatcher via the getter right after. The instance is
    // per-hunter, so this caching is safe for one entity per tick.
    private int currentPriority = casualPriority;
    public int Priority => currentPriority;

    public HuntForStockpileBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // All hunter parts in place? Backpack has room? Not too tired? Stockpile
    // not full? Reachable prey? Also computes the current priority tier based
    // on stockpile fill, so the dispatcher's next read picks up the right
    // urgency.
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Predator>(id) || !World.HasComponent<Melee>(id)) return false;
        if (!World.HasComponent<Position>(id) || !World.HasComponent<Container>(id)) return false;

        // Keep hunting as long as the pack has any room. Hunter rolls up
        // multiple kills per trip until full, then returns to deposit.
        if (World.GetComponent<Container>(id).Free <= 0) return false;

        // Too tired — head home to eat instead of chasing one more rabbit
        // into death. Without this, hunting priority can override walking-home.
        if (World.HasComponent<Energy>(id))
        {
            Energy e = World.GetComponent<Energy>(id);
            if (e.Current < e.Max * tooTiredFraction) return false;
        }

        // Stockpile fill decides both whether to fire AND how urgent the hunt
        // is. A full stockpile means stay home; anything less is fair game,
        // with priority scaling by how low it's running.
        Container? storage = HunterHelpers.CampStorage(id);
        if (storage == null) return false;
        double fill = storage.CountOf(Resources.Meat) / (double)storage.capacity;
        if (fill >= 1.0) return false;

        if (fill < lowFill) currentPriority = criticalPriority;
        else if (fill < routineFill) currentPriority = urgentPriority;
        else if (fill < comfortableFill) currentPriority = routinePriority;
        else currentPriority = casualPriority;

        // Same hunt mechanic as HuntBehavior. Duplicated inline rather than
        // extracted — two callers is borderline for extraction; a third
        // would justify a shared helper.
        Predator predator = World.GetComponent<Predator>(id);
        Position pos = World.GetComponent<Position>(id);
        int range = StatMath.VisionRange(id);

        List<FlowField> fields = new List<FlowField>();
        foreach (Func<int, int, int> preySpawn in predator.preySpecies)
            fields.Add(World.GetSpeciesFlowField(preySpawn));

        if (!FlowFieldHelper.PickNearestNeighborStep(pos.X, pos.Y, fields, range, rng, out FlowFieldStep step))
            return false;

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
    // Adjacent: strike via Melee, grapple survivors (cost 3). Walking: step
    // toward prey (cost 1). Same mechanic as HuntBehavior but without the
    // raid-wolf "hasKilled" flag.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        if (cachedPreyAdjacent)
        {
            // Capture prey state BEFORE anything destructive — DeathHelper
            // will destroy the entity below, and "attacks ?" in the log
            // comes from Label() falling back to the name-fallback once
            // the entity is gone.
            Position preyPos = World.GetComponent<Position>(cachedPreyId);
            int preyX = preyPos.X, preyY = preyPos.Y;
            bool wasPinned = World.HasComponent<Grappled>(cachedPreyId);

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

            if (killed)
            {
                // Step onto the corpse cell. Being Solid on the kill blocks other
                // predators from poaching it, and next tick's Butcher underfoot
                // check finds it naturally — without this the hunter leaves fresh
                // kills behind when an older walk-pursuit is still attached.
                Position mine = World.GetComponent<Position>(id);
                MovementHelper.TryMove(id, preyX - mine.X, preyY - mine.Y);
            }
            else
            {
                World.AttachComponent(cachedPreyId, new Grappled { attackerId = id });
            }

            return 3;
        }

        MovementHelper.TryMove(id, cachedStepDx, cachedStepDy);
        return 1;
    }
}

// ----------------------------------------------------------------------------
// Behavior: anywhere but camp and nothing else to do — walk home. Low
// priority so everything purposeful gets first dibs; when they all decline,
// the hunter heads home.
//
// Attaches a NavigatePursuit to the camp's cell at priority 3 (casual —
// almost anything preempts, including "a corpse walked past me"). Self-gates
// while a pursuit is already running so the hunter doesn't re-commit to
// the same walk every tick.
// ----------------------------------------------------------------------------
public class ReturnToCampBehavior : IBehavior
{
    public int Priority => 4;

    private Random rng;

    public ReturnToCampBehavior(Random rng)
    {
        this.rng = rng;
    }

    // ----------------------------------------------------------------------------
    // Not at camp, not already walking, camp still exists?
    // ----------------------------------------------------------------------------
    public bool WouldAct(int id)
    {
        if (!World.HasComponent<Home>(id) || !World.HasComponent<Position>(id)) return false;
        if (HunterHelpers.AtCamp(id)) return false;
        if (World.HasComponent<Pursuit>(id)) return false;

        int campId = World.GetComponent<Home>(id).campId;
        if (!World.EntityExists(campId) || !World.HasComponent<Position>(campId)) return false;
        return true;
    }

    // ----------------------------------------------------------------------------
    // Attach a NavigatePursuit toward the camp. Cost 0 so the dispatcher runs
    // the pursuit's first step this same tick.
    // ----------------------------------------------------------------------------
    public int Act(int id)
    {
        int campId = World.GetComponent<Home>(id).campId;
        Position camp = World.GetComponent<Position>(campId);
        World.AttachComponent(id, new Pursuit(new NavigatePursuit(camp.X, camp.Y, priority: 3, rng)));
        World.Log($"{World.Label(id)} walks home to camp");
        return 0;
    }
}
