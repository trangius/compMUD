namespace Engine;

// ----------------------------------------------------------------------------
// Optional lifecycle hooks a component can implement to react to being added
// to / removed from an entity. AttachComponent and DetachComponent call these
// — components that don't implement them pay nothing. Use these when a
// component must stay in sync with some world-level index or other out-of-band
// state (Position ↔ spatial index; schedulers seeding NextActTick). If a
// component is just data, don't implement them.
// ----------------------------------------------------------------------------

public interface IOnAttach
{
    void OnAttach(int id);
}

public interface IOnDetach
{
    void OnDetach(int id);
}

// ----------------------------------------------------------------------------
// The world — all entities live here. Create them, destroy them, find them,
// ask what's at a cell. Entities are integer IDs with attached components.
// ----------------------------------------------------------------------------
public static class World
{
    private static int nextEntityId = 0;

    // The live entity ids. An entity is just an int; this says which ints exist.
    private static HashSet<int> entities = new HashSet<int>();

    // All entity data. components[typeof(Position)][42] is entity 42's Position.
    // A missing inner key means that entity doesn't have that component.
    private static Dictionary<Type, Dictionary<int, object>> components = new Dictionary<Type, Dictionary<int, object>>();

    // Cell (x,y) → entity ids there. Makes spatial queries O(1).
    // Position keeps this in sync via its IOnAttach/IOnDetach hooks; MoveEntity
    // re-attaches. Don't write components[Position] directly — see engine.spatial-index.md.
    private static Dictionary<(int, int), List<int>> spatialIndex = new Dictionary<(int, int), List<int>>();

    // Per-tick flow-field cache — one precomputed "distance and direction to
    // nearest X" map per perception target, shared by every consumer that tick.
    // Key is the selector that defines "X" (species spawn delegate for
    // GetSpeciesFlowField; other lookups can use different key types). Cleared
    // at the top of Tick so positions go stale at a tick boundary, never
    // mid-tick. See GetSpeciesFlowField below.
    private static Dictionary<object, FlowField> flowFieldCache = new Dictionary<object, FlowField>();

    public static int mapWidth;
    public static int mapHeight;
    public static int tickCount;
    public static int EntityCount => entities.Count;
    public static List<string> messageLog = new List<string>();

    // ----------------------------------------------------------------------------
    // Set the world's dimensions. Call an Area builder (e.g. Area.StartingArea)
    // afterwards from Game1 to populate terrain and entities.
    // ----------------------------------------------------------------------------
    public static void Initialize(int width, int height)
    {
        mapWidth = width;
        mapHeight = height;
    }

    // ----------------------------------------------------------------------------
    // Wipe the world back to empty so a frontend can rebuild a fresh area
    // without restarting the process. Used by the Console bench harness to swap
    // between StartingArea and StressArea in one session. Resets all the static
    // state — entity ids, components, indexes, tick counter, log.
    // ----------------------------------------------------------------------------
    public static void Reset()
    {
        nextEntityId = 0;
        entities.Clear();
        components.Clear();
        spatialIndex.Clear();
        flowFieldCache.Clear();
        tickCount = 0;
        messageLog.Clear();
    }

    // ----------------------------------------------------------------------------
    // Advance the world by one step. Two passes in order:
    //   1. Actions — each entity DUE this tick (per its IScheduler) picks ONE
    //      behavior and runs it. AgilityPaced derives its period from Stats;
    //      FixedPaced uses a literal period. Entities with no scheduler fall
    //      back to "act every tick".
    //   2. Effects — run on every entity every tick regardless of pace (wall-
    //      clock drain/decay/aging). Effects don't know about schedulers.
    // ----------------------------------------------------------------------------
    public static void Tick()
    {
        // Wipe last tick's flow-field scratchpad. Entries are positional maps
        // built from creature positions at the moment of the cache miss; they
        // go stale the instant anyone moves.
        flowFieldCache.Clear();

        // Pass 1: gated behavior dispatch
        foreach (int id in AllWithComponent<Behaviors>())
        {
            if (!EntityExists(id)) continue;

            // Scheduler gates the turn — if this entity isn't due yet, skip
            IScheduler? sched = Scheduling.Get(id);
            if (sched != null && !sched.IsDue(tickCount))
                continue;

            // Grapple auto-release: if the attacker is gone or no longer adjacent,
            // drop the stale Grappled state so the victim can act normally again.
            if (HasComponent<Grappled>(id) && !GetComponent<Grappled>(id).IsStillValid(id))
                DetachComponent<Grappled>(id);

            // Pursuit cleanup: a completed pursuit detaches before reactive
            // evaluation so the caller-behavior can fire fresh this tick.
            if (HasComponent<Pursuit>(id) && GetComponent<Pursuit>(id).current.IsComplete(id))
                DetachComponent<Pursuit>(id);

            // Grappled entities run only behaviors that opt in via ICanActWhenGrappled
            bool isGrappled = HasComponent<Grappled>(id);

            // Ask every behavior if it wants to act; remember the highest-priority yes
            IBehavior? winner = null;
            int bestPriority = int.MinValue;
            foreach (IBehavior b in GetComponent<Behaviors>(id).list)
            {
                if (isGrappled && !(b is ICanActWhenGrappled)) continue;
                if (b.WouldAct(id) && b.Priority > bestPriority)
                {
                    winner = b;
                    bestPriority = b.Priority;
                }
            }

            // Decide who drives this tick: reactive winner, pursuit, or nobody.
            // Grappled entities can't advance a pursuit (same as moving).
            bool pursuitActive = !isGrappled && HasComponent<Pursuit>(id);
            int cost;
            if (winner != null && (!pursuitActive || bestPriority > GetComponent<Pursuit>(id).Priority))
            {
                // Reactive winner takes the tick (either no pursuit, or winner outranks it).
                cost = winner.Act(id);
                // Immediate pursuit: if Act just attached a pursuit and took no
                // cost, let the pursuit take its first step this same tick.
                if (cost == 0 && EntityExists(id) && HasComponent<Pursuit>(id))
                    cost = GetComponent<Pursuit>(id).current.Step(id);
            }
            else if (pursuitActive)
            {
                // No preempting winner; the pursuit advances.
                cost = GetComponent<Pursuit>(id).current.Step(id);
            }
            else
            {
                // Nothing wants to act; baseline cost 1.
                cost = 1;
            }

            // Push the entity's next action forward by period × cost. Skip if
            // the entity no longer exists — a behavior may have destroyed it.
            if (EntityExists(id) && sched != null)
                sched.Reschedule(tickCount, cost, id);
        }

        // Pass 2: passive effects fire every tick on every entity (wall-clock)
        foreach (int id in AllWithComponent<Effects>())
        {
            if (!EntityExists(id)) continue;
            foreach (IEffect ef in GetComponent<Effects>(id).list)
            {
                if (!EntityExists(id)) break;  // an earlier effect may have killed the host
                ef.Apply(id);
            }
        }

        tickCount++;
    }

    // ----------------------------------------------------------------------------
    // Add a message to the log. Keeps only the most recent 50.
    // ----------------------------------------------------------------------------
    public static void Log(string message)
    {
        messageLog.Insert(0, message);
        if (messageLog.Count > 50)
            messageLog.RemoveAt(50);
    }

    // ----------------------------------------------------------------------------
    // Get an entity's display name, or a fallback if it has none.
    // ----------------------------------------------------------------------------
    public static string GetEntityName(int id, string fallback = "?")
    {
        return HasComponent<Named>(id) ? GetComponent<Named>(id).name : fallback;
    }

    // ----------------------------------------------------------------------------
    // "Name id" label for log lines — lets you match a log entry to the same
    // id shown on the map and in the debug sidebar. e.g. "Rabbit 42".
    // ----------------------------------------------------------------------------
    public static string Label(int id)
    {
        return $"{GetEntityName(id)} {id}";
    }

    // ----------------------------------------------------------------------------
    // Create a new entity. Returns its ID.
    // ----------------------------------------------------------------------------
    public static int CreateEntity()
    {
        int id = nextEntityId++;
        entities.Add(id);
        return id;
    }
    // ----------------------------------------------------------------------------
    // Destroy an entity and all its components from the world. Each component
    // gets an OnDetach call before it's dropped so things like the spatial
    // index (hooked by Position) stay in sync.
    // ----------------------------------------------------------------------------
    public static void DestroyEntity(int id)
    {
        // Walk every component store; fire OnDetach on anything that cares
        foreach (Dictionary<int, object> store in components.Values)
        {
            if (store.TryGetValue(id, out object? c) && c is IOnDetach hook)
                hook.OnDetach(id);
            store.Remove(id);
        }

        entities.Remove(id);
    }

    // ----------------------------------------------------------------------------
    // Attach a component to an entity. Replaces if one of that type already exists.
    // Old component (if any) gets OnDetach; new component gets OnAttach — components
    // that don't implement either interface pay nothing.
    // ----------------------------------------------------------------------------
    public static void AttachComponent<T>(int id, T component) where T : class
    {
        Type type = typeof(T);

        if (!components.ContainsKey(type))
            components[type] = new Dictionary<int, object>();

        // If replacing an existing component, let the old one tear down first
        if (components[type].TryGetValue(id, out object? old) && old is IOnDetach od)
            od.OnDetach(id);

        components[type][id] = component;

        // Let the new component register itself against the world
        if (component is IOnAttach oa)
            oa.OnAttach(id);
    }

    // ----------------------------------------------------------------------------
    // Detach a component type from an entity. Fires OnDetach first so hooks
    // (e.g. Position removing itself from the spatial index) run before the
    // component disappears.
    // ----------------------------------------------------------------------------
    public static void DetachComponent<T>(int id) where T : class
    {
        Type type = typeof(T);
        if (!components.ContainsKey(type)) return;

        if (components[type].TryGetValue(id, out object? c) && c is IOnDetach od)
            od.OnDetach(id);

        components[type].Remove(id);
    }

    // ----------------------------------------------------------------------------
    // Get a component from an entity. Throws if the entity doesn't have it.
    // ----------------------------------------------------------------------------
    public static T GetComponent<T>(int id) where T : class
    {
        return (T)components[typeof(T)][id];
    }

    // ----------------------------------------------------------------------------
    // Check if an entity has a component of this type.
    // ----------------------------------------------------------------------------
    public static bool HasComponent<T>(int id) where T : class
    {
        Type type = typeof(T);
        return components.ContainsKey(type) && components[type].ContainsKey(id);
    }

    // ----------------------------------------------------------------------------
    // Get all (the worlds)entity IDs that have a component of this type. Returns
    // a snapshot.
    // ----------------------------------------------------------------------------
    public static List<int> AllWithComponent<T>() where T : class
    {
        Type type = typeof(T);
        if (!components.ContainsKey(type)) return new List<int>();
        return components[type].Keys.ToList();
    }

    // ----------------------------------------------------------------------------
    // Same as above but with two components
    // ----------------------------------------------------------------------------
    public static List<int> AllWithComponents<T1, T2>() where T1 : class where T2 : class
    {
        Type type1 = typeof(T1);
        Type type2 = typeof(T2);

        if (!components.ContainsKey(type1) || !components.ContainsKey(type2))
            return new List<int>();

        // Iterate the smaller set for efficiency
        Dictionary<int, object> smaller, larger;
        if (components[type1].Count <= components[type2].Count)
        {
            smaller = components[type1];
            larger = components[type2];
        }
        else
        {
            smaller = components[type2];
            larger = components[type1];
        }

        List<int> result = new List<int>();
        foreach (int id in smaller.Keys)
        {
            if (larger.ContainsKey(id))
                result.Add(id);
        }
        return result;
    }

    // ----------------------------------------------------------------------------
    // Get all entities at a cell. Returns a copy so callers can safely modify.
    // ----------------------------------------------------------------------------
    public static List<int> EntitiesAt(int x, int y)
    {
        if (spatialIndex.TryGetValue((x, y), out List<int>? list))
            return new List<int>(list);
        return new List<int>();
    }

    // ----------------------------------------------------------------------------
    // Move an entity to a new cell. Goes through the normal attach/detach path
    // so Position's hooks keep the spatial index in sync — no direct writes.
    // ----------------------------------------------------------------------------
    public static void MoveEntity(int id, int newX, int newY)
    {
        DetachComponent<Position>(id);
        AttachComponent(id, new Position(newX, newY));
    }

    // ----------------------------------------------------------------------------
    // Check if an entity is still alive.
    // ----------------------------------------------------------------------------
    public static bool EntityExists(int id)
    {
        return entities.Contains(id);
    }

    // ----------------------------------------------------------------------------
    // Scan a circle around (x,y) and return the closest entity matching the filter.
    // Returns -1 if nothing found.
    // ----------------------------------------------------------------------------
    public static int FindNearestEntity(int x, int y, int range, Func<int, bool> filter)
    {
        int bestId = -1;
        int bestDistSq = int.MaxValue;
        int rangeSq = range * range;

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq > rangeSq) continue;

                foreach (int id in EntitiesAt(x + dx, y + dy))
                {
                    if (filter(id) && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestId = id;
                    }
                }
            }
        }
        return bestId;
    }

    // ----------------------------------------------------------------------------
    // Check if a cell is bare walkable ground with nothing built on it.
    // ----------------------------------------------------------------------------
    public static bool IsOpenGround(int x, int y)
    {
        bool hasWalkable = false;
        foreach (int id in EntitiesAt(x, y))
        {
            if (HasComponent<Walkable>(id)) hasWalkable = true;
            if (HasComponent<Appearance>(id) && GetComponent<Appearance>(id).layer >= 2) return false;
        }
        return hasWalkable;
    }

    // ----------------------------------------------------------------------------
    // Cell predicate — a creature can be placed here: walkable ground, no Solid.
    // Trees/bushes pass (not Solid). Other creatures fail (Solid). Walls fail (not walkable).
    // TODO: Make general, not just for walkable. What about fishes who spawn in water?
    // ----------------------------------------------------------------------------
    public static bool CanCreatureBeHere(int x, int y)
    {
        bool hasWalkable = false;
        foreach (int id in EntitiesAt(x, y))
        {
            if (HasComponent<Solid>(id)) return false;
            if (HasComponent<Walkable>(id)) hasWalkable = true;
        }
        return hasWalkable;
    }

    // ----------------------------------------------------------------------------
    // Pick a random cell that matches the predicate. Returns (-1,-1) if none found.
    // Caller supplies the rule — CanCreatureBeHere, IsOpenGround, future IsInWater, etc.
    // ----------------------------------------------------------------------------
    public static (int x, int y) FindCell(Func<int, int, bool> cellAccepts, Random rng, int attempts = 100)
    {
        for (int i = 0; i < attempts; i++)
        {
            int x = rng.Next(2, mapWidth - 2);
            int y = rng.Next(2, mapHeight - 2);
            if (cellAccepts(x, y)) return (x, y);
        }
        return (-1, -1);
    }

    // ----------------------------------------------------------------------------
    // Spatial index maintenance. Internal — only Position's OnAttach/OnDetach
    // are allowed to call these. Anything else goes through AttachComponent /
    // DetachComponent / MoveEntity. See engine.spatial-index.md.
    // ----------------------------------------------------------------------------
    internal static void AddToSpatialIndex(int id, int x, int y)
    {
        var key = (x, y);
        if (!spatialIndex.ContainsKey(key))
            spatialIndex[key] = new List<int>();
        spatialIndex[key].Add(id);
    }

    internal static void RemoveFromSpatialIndex(int id, int x, int y)
    {
        var key = (x, y);
        if (spatialIndex.TryGetValue(key, out List<int>? list))
        {
            list.Remove(id);
            if (list.Count == 0)
                spatialIndex.Remove(key);
        }
    }

    // ----------------------------------------------------------------------------
    // Get this tick's flow field seeded from every cell holding an entity of
    // the given species. First caller computes; subsequent callers reuse —
    // every wolf hunting rabbits asks the same question, so one flood covers
    // the whole pack.
    //
    // Cache miss: iterate all Species-bearing entities, pick the ones whose
    // spawn delegate matches, collect their Positions, flood once. Seed cells
    // themselves are Solid (the creatures ARE the seeds) but MultiSourceBFS
    // exempts seeds from the passability check.
    // ----------------------------------------------------------------------------
    public static FlowField GetSpeciesFlowField(Func<int, int, int> spawn)
    {
        if (flowFieldCache.TryGetValue(spawn, out FlowField? cached)) return cached;

        // Collect every position where this species sits.
        List<(int x, int y)> seeds = new List<(int, int)>();
        foreach (int id in AllWithComponent<Species>())
        {
            if (GetComponent<Species>(id).spawn != spawn) continue;
            if (!HasComponent<Position>(id)) continue;
            Position p = GetComponent<Position>(id);
            seeds.Add((p.X, p.Y));
        }

        FlowField fresh = Algorithms.MultiSourceBFS(seeds, CanCreatureBeHere);
        flowFieldCache[spawn] = fresh;
        return fresh;
    }

    // ----------------------------------------------------------------------------
    // Get this tick's flow field seeded from every cell holding an edible
    // source of the given resource category — a Yields entity with an unspent
    // entry of that category and no Health (live creatures aren't food, Hunt
    // handles them). Rabbits reading Berry get bushes; wolves reading Meat
    // get corpses. Partly-drained corpses still appear in fields for whichever
    // categories they still carry, so a picked-clean corpse drops out of Meat
    // but stays in Pelt.
    // ----------------------------------------------------------------------------
    public static FlowField GetYieldFlowField(ResourceCategory category)
    {
        if (flowFieldCache.TryGetValue(category, out FlowField? cached)) return cached;

        // Walk every Yields entity; include those carrying the category and
        // not alive. Same filter FeedBehavior.IsEdible applies, but now at
        // seed time — the flood itself is what the consumer reads.
        List<(int x, int y)> seeds = new List<(int, int)>();
        foreach (int id in AllWithComponent<Yields>())
        {
            if (HasComponent<Health>(id)) continue;
            if (!HasComponent<Position>(id)) continue;
            if (GetComponent<Yields>(id).Get(category) == null) continue;
            Position p = GetComponent<Position>(id);
            seeds.Add((p.X, p.Y));
        }

        FlowField fresh = Algorithms.MultiSourceBFS(seeds, CanCreatureBeHere);
        flowFieldCache[category] = fresh;
        return fresh;
    }

    // ----------------------------------------------------------------------------
    // Get this tick's flow field seeded from every cell holding a predator
    // whose prey list includes the given species. Used by RunFromPredator —
    // a rabbit asks "where are the things that hunt me?" and gets the union,
    // then steps to the neighbor with largest distance (away from the pack).
    //
    // Cache key is a composite — a plain species delegate means "this species'
    // members" (GetSpeciesFlowField); wrapping it in a PredatorsHunting tag
    // means "predators who hunt this species" — distinct cache entries.
    // ----------------------------------------------------------------------------
    public static FlowField GetPredatorsHuntingFlowField(Func<int, int, int> preySpawn)
    {
        object key = new PredatorsHuntingKey(preySpawn);
        if (flowFieldCache.TryGetValue(key, out FlowField? cached)) return cached;

        List<(int x, int y)> seeds = new List<(int, int)>();
        foreach (int id in AllWithComponent<Predator>())
        {
            if (!GetComponent<Predator>(id).Hunts(preySpawn)) continue;
            if (!HasComponent<Position>(id)) continue;
            Position p = GetComponent<Position>(id);
            seeds.Add((p.X, p.Y));
        }

        FlowField fresh = Algorithms.MultiSourceBFS(seeds, CanCreatureBeHere);
        flowFieldCache[key] = fresh;
        return fresh;
    }

    // Key wrapper so "predators hunting X" caches separately from "members of
    // species X" in flowFieldCache. Equality is by the wrapped spawn delegate.
    private sealed record PredatorsHuntingKey(Func<int, int, int> PreySpawn);

    // ----------------------------------------------------------------------------
    // Get this tick's flow field seeded from every cell holding any entity
    // with the given component. Used by ReturnToForest (<Tree>) — the generic
    // shape covers any future marker component people might want to path
    // toward (<Fire>, <Altar>, <Quest>, ...). Cache key is the Type so two
    // different T's don't collide.
    // ----------------------------------------------------------------------------
    public static FlowField GetComponentFlowField<T>() where T : class
    {
        object key = typeof(T);
        if (flowFieldCache.TryGetValue(key, out FlowField? cached)) return cached;

        List<(int x, int y)> seeds = new List<(int, int)>();
        foreach (int id in AllWithComponent<T>())
        {
            if (!HasComponent<Position>(id)) continue;
            Position p = GetComponent<Position>(id);
            seeds.Add((p.X, p.Y));
        }

        FlowField fresh = Algorithms.MultiSourceBFS(seeds, CanCreatureBeHere);
        flowFieldCache[key] = fresh;
        return fresh;
    }

    // ----------------------------------------------------------------------------
    // Get this tick's flow field seeded from a single cell. The pathing
    // primitive for NavigatePursuit — "one step toward (x, y)." Cache is
    // keyed by (x, y) as a ValueTuple, so multiple consumers heading to the
    // same goal cell share the same flood.
    // ----------------------------------------------------------------------------
    public static FlowField GetCellFlowField(int x, int y)
    {
        object key = (x, y);
        if (flowFieldCache.TryGetValue(key, out FlowField? cached)) return cached;

        FlowField fresh = Algorithms.MultiSourceBFS(new[] { (x, y) }, CanCreatureBeHere);
        flowFieldCache[key] = fresh;
        return fresh;
    }
}
