namespace Engine;

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
    // Stay in sync via MoveEntity / AttachComponent / DetachComponent — don't write components[Position] directly.
    private static Dictionary<(int, int), List<int>> spatialIndex = new Dictionary<(int, int), List<int>>();

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
    // Advance the world by one step: each entity picks one action, then energy drains.
    // ----------------------------------------------------------------------------
    public static void Tick()
    {
        // Every entity picks ONE action per tick — highest-priority willing behavior wins
        foreach (int id in AllWithComponent<Behaviors>())
        {
            if (!EntityExists(id)) continue;

            // Ask every behavior if it wants to act; remember the highest-priority yes
            IBehavior? winner = null;
            int bestPriority = int.MinValue;
            foreach (IBehavior b in GetComponent<Behaviors>(id).list)
            {
                if (b.WouldAct(id) && b.Priority > bestPriority)
                {
                    winner = b;
                    bestPriority = b.Priority;
                }
            }

            // Only the winner changes world state this tick
            winner?.Act(id);
        }

        // Passive effects: run every effect on every entity (no competition, no priority)
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
    // Create a new entity. Returns its ID.
    // ----------------------------------------------------------------------------
    public static int CreateEntity()
    {
        int id = nextEntityId++;
        entities.Add(id);
        return id;
    }
    // ----------------------------------------------------------------------------
    // Destroy an entity and all its components from the world.
    // ----------------------------------------------------------------------------
    public static void DestroyEntity(int id)
    {
        if (HasComponent<Position>(id))
        {
            Position pos = GetComponent<Position>(id);
            RemoveFromSpatialIndex(id, pos.X, pos.Y);
        }

        foreach (Dictionary<int, object> store in components.Values)
            store.Remove(id);

        entities.Remove(id);
    }

    // ----------------------------------------------------------------------------
    // Attach a component to an entity. Replaces if one of that type already exists.
    // ----------------------------------------------------------------------------
    public static void AttachComponent<T>(int id, T component) where T : class
    {
        Type type = typeof(T);

        if (!components.ContainsKey(type))
            components[type] = new Dictionary<int, object>();

        // Keep the spatial index in sync when adding a Position
        if (component is Position newPos)
        {
            if (HasComponent<Position>(id))
            {
                Position oldPos = GetComponent<Position>(id);
                RemoveFromSpatialIndex(id, oldPos.X, oldPos.Y);
            }
            AddToSpatialIndex(id, newPos.X, newPos.Y);
        }

        components[type][id] = component;
    }

    // ----------------------------------------------------------------------------
    // Detach a component type from an entity.
    // ----------------------------------------------------------------------------
    public static void DetachComponent<T>(int id) where T : class
    {
        Type type = typeof(T);
        if (!components.ContainsKey(type)) return;

        if (type == typeof(Position) && components[type].ContainsKey(id))
        {
            Position pos = (Position)components[type][id];
            RemoveFromSpatialIndex(id, pos.X, pos.Y);
        }

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
    // Move an entity to a new cell. Keeps the spatial index in sync.
    // ----------------------------------------------------------------------------
    public static void MoveEntity(int id, int newX, int newY)
    {
        Position pos = GetComponent<Position>(id);
        RemoveFromSpatialIndex(id, pos.X, pos.Y);
        components[typeof(Position)][id] = new Position(newX, newY);
        AddToSpatialIndex(id, newX, newY);
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
    public static bool IsCreatureSpawnable(int x, int y)
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
    // Caller supplies the rule — IsCreatureSpawnable, IsOpenGround, future IsInWater, etc.
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

    private static void AddToSpatialIndex(int id, int x, int y)
    {
        var key = (x, y);
        if (!spatialIndex.ContainsKey(key))
            spatialIndex[key] = new List<int>();
        spatialIndex[key].Add(id);
    }

    private static void RemoveFromSpatialIndex(int id, int x, int y)
    {
        var key = (x, y);
        if (spatialIndex.TryGetValue(key, out List<int>? list))
        {
            list.Remove(id);
            if (list.Count == 0)
                spatialIndex.Remove(key);
        }
    }
}
