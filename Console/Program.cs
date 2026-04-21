using System.Diagnostics;
using Engine;
Console.WriteLine("test123");
// Sprite registry — maps spriteId to console glyph and color
Dictionary<string, (string glyph, ConsoleColor color)> sprites = new()
{
    ["grass"] = (".", ConsoleColor.Green),
    ["wall"] = ("#", ConsoleColor.Gray),
    ["water"] = ("~", ConsoleColor.Blue),
    ["tree"] = ("T", ConsoleColor.DarkGreen),
    ["bush"] = ("*", ConsoleColor.DarkYellow),
    ["rabbit"] = ("r", ConsoleColor.White),
    ["wolf"] = ("W", ConsoleColor.Red),
    ["hunter"] = ("H", ConsoleColor.Yellow),
    ["camp"] = ("A", ConsoleColor.DarkYellow),
    ["corpse"] = ("%", ConsoleColor.Magenta),
    ["bones"] = ("%", ConsoleColor.Gray),
};

// Pick the area from argv. Default = StartingArea (the regular sandbox).
// `dotnet run -- stress [rabbits] [wolves] [bushes]` builds a packed bench map
// on a bigger grid for measuring tick cost.
if (args.Length > 0 && args[0] == "stress")
{
    int sr = args.Length > 1 && int.TryParse(args[1], out int p1) ? p1 : 200;
    int sw = args.Length > 2 && int.TryParse(args[2], out int p2) ? p2 : 50;
    int sb = args.Length > 3 && int.TryParse(args[3], out int p3) ? p3 : 400;
    World.Initialize(120, 60);
    StressArea.Build(sr, sw, sb);
    Console.WriteLine($"Stress area: {World.mapWidth}x{World.mapHeight}, {sr} rabbits, {sw} wolves, {sb} bushes, {World.EntityCount} entities total");
}
else
{
    World.Initialize(60, 30);
    HomeArea.StartingArea();
    Console.WriteLine($"Map generated: {World.mapWidth}x{World.mapHeight}, {World.EntityCount} entities");
}
Console.WriteLine("Commands: look, tick [n], bench <n>, stress [r] [w] [b], status, log, info <x> <y>, pursue <id> <x> <y> [priority], quit");
Console.WriteLine();

// Shared rng for dev/test commands that need one (e.g. pursue). Fixed seed
// so scripted runs stay reproducible.
Random devRng = new Random(1);

RenderMap(sprites);

// --- Command loop ---
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    string command = parts[0].ToLower();

    if (command == "quit" || command == "q")
        break;

    if (command == "look")
    {
        RenderMap(sprites);
    }
    else if (command == "tick" || command == "t")
    {
        int count = 1;
        if (parts.Length >= 2) int.TryParse(parts[1], out count);
        count = Math.Max(1, count);

        for (int i = 0; i < count; i++)
            World.Tick();

        Console.WriteLine($"Turn {World.tickCount}");
        RenderMap(sprites);
    }
    else if (command == "bench" && parts.Length >= 2)
    {
        // Time N ticks with no rendering between them. Reports total ms,
        // ms/tick, ticks/sec, and final entity count so you can tell whether
        // the population shifted during the run.
        if (!int.TryParse(parts[1], out int benchTicks) || benchTicks <= 0)
        {
            Console.WriteLine("Usage: bench <ticks>");
        }
        else
        {
            int startEntities = World.EntityCount;
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchTicks; i++) World.Tick();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            double msPerTick = ms / benchTicks;
            double tps = benchTicks / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"Bench: {benchTicks} ticks in {ms:F1} ms — {msPerTick:F3} ms/tick — {tps:F0} ticks/sec");
            Console.WriteLine($"  Entities: {startEntities} → {World.EntityCount}  (turn {World.tickCount})");
        }
    }
    else if (command == "stress")
    {
        // Rebuild the world as a fresh stress map without restarting the binary.
        int sr = parts.Length > 1 && int.TryParse(parts[1], out int p1) ? p1 : 200;
        int sw = parts.Length > 2 && int.TryParse(parts[2], out int p2) ? p2 : 50;
        int sb = parts.Length > 3 && int.TryParse(parts[3], out int p3) ? p3 : 400;
        World.Reset();
        World.Initialize(120, 60);
        StressArea.Build(sr, sw, sb);
        Console.WriteLine($"Stress area rebuilt: {sr} rabbits, {sw} wolves, {sb} bushes, {World.EntityCount} entities total");
    }
    else if (command == "status")
    {
        ShowStatus();
    }
    else if (command == "log")
    {
        int count = Math.Min(20, World.messageLog.Count);
        for (int i = 0; i < count; i++)
            Console.WriteLine($"  {World.messageLog[i]}");
        if (count == 0) Console.WriteLine("  No events yet.");
    }
    else if (command == "info" && parts.Length >= 3)
    {
        if (int.TryParse(parts[1], out int ix) && int.TryParse(parts[2], out int iy))
            ShowCellInfo(ix, iy);
        else
            Console.WriteLine("Usage: info <x> <y>");
    }
    else if (command == "pursue" && parts.Length >= 4)
    {
        // Dev hook — attach a NavigatePursuit to any entity so we can watch
        // the pursuit layer run. Optional fourth arg overrides the default
        // priority (3 — about idle-wander level; Run/Feed/etc preempt naturally).
        if (!int.TryParse(parts[1], out int pId) || !int.TryParse(parts[2], out int pX) || !int.TryParse(parts[3], out int pY))
        {
            Console.WriteLine("Usage: pursue <id> <x> <y> [priority]");
        }
        else if (!World.EntityExists(pId))
        {
            Console.WriteLine($"Entity {pId} doesn't exist.");
        }
        else
        {
            int prio = parts.Length >= 5 && int.TryParse(parts[4], out int p) ? p : 3;
            World.AttachComponent(pId, new Pursuit(new NavigatePursuit(pX, pY, prio, devRng)));
            Console.WriteLine($"Entity {pId} now pursuing ({pX},{pY}) at priority {prio}");
        }
    }
    else
    {
        Console.WriteLine("Unknown command. Try: look, tick [n], bench <n>, stress [r] [w] [b], status, log, info <x> <y>, pursue <id> <x> <y> [priority], quit");
    }
}

static void RenderMap(Dictionary<string, (string glyph, ConsoleColor color)> sprites)
{
    for (int y = 0; y < World.mapHeight; y++)
    {
        for (int x = 0; x < World.mapWidth; x++)
        {
            List<int> atCell = World.EntitiesAt(x, y);

            string glyph = " ";
            ConsoleColor color = ConsoleColor.White;
            int topLayer = -1;

            foreach (int id in atCell)
            {
                if (World.HasComponent<Appearance>(id))
                {
                    Appearance app = World.GetComponent<Appearance>(id);
                    if (app.layer > topLayer && sprites.ContainsKey(app.spriteId))
                    {
                        topLayer = app.layer;
                        glyph = sprites[app.spriteId].glyph;
                        color = sprites[app.spriteId].color;
                    }
                }
            }

            Console.ForegroundColor = color;
            Console.Write(glyph);
        }
        Console.WriteLine();
    }
    Console.ResetColor();
}

static void ShowStatus()
{
    int rabbits = 0, wolves = 0, hunters = 0;

    foreach (int id in World.AllWithComponent<Named>())
    {
        string name = World.GetComponent<Named>(id).name;
        if (name == "Rabbit") rabbits++;
        else if (name == "Wolf") wolves++;
        else if (name == "Hunter") hunters++;
    }

    int corpses = World.AllWithComponent<Corpse>().Count;
    int vegetation = World.AllWithComponent<Vegetation>().Count;

    Console.WriteLine($"Turn {World.tickCount} — Rabbits: {rabbits}  Wolves: {wolves}  Hunters: {hunters}  Corpses: {corpses}  Vegetation: {vegetation}");

    // One line per container in the world — backpack on a creature, stockpile
    // at a camp. Quick glance at what's being carried / stored.
    foreach (int id in World.AllWithComponent<Container>())
    {
        Container c = World.GetComponent<Container>(id);
        string kind = World.HasComponent<Camp>(id) ? "Camp" : "Pack";
        string contents = c.stacks.Count == 0
            ? "empty"
            : string.Join(", ", c.stacks.Select(s => $"{s.category.name} {s.amount}"));
        Console.WriteLine($"  {kind} {World.Label(id)}: {contents} ({c.Used}/{c.capacity})");
    }
}

static void ShowCellInfo(int x, int y)
{
    List<int> atCell = World.EntitiesAt(x, y);
    if (atCell.Count == 0)
    {
        Console.WriteLine($"Nothing at ({x},{y})");
        return;
    }

    Console.WriteLine($"Entities at ({x},{y}):");
    foreach (int id in atCell)
    {
        List<string> tags = new List<string>();

        if (World.HasComponent<Named>(id)) tags.Add(World.GetComponent<Named>(id).name);
        if (World.HasComponent<Appearance>(id))
        {
            Appearance app = World.GetComponent<Appearance>(id);
            tags.Add($"sprite:{app.spriteId} layer:{app.layer}");
        }
        if (World.HasComponent<Health>(id))
        {
            Health h = World.GetComponent<Health>(id);
            tags.Add($"HP:{h.Current}/{h.Max}");
        }
        if (World.HasComponent<Energy>(id))
        {
            Energy e = World.GetComponent<Energy>(id);
            tags.Add($"Energy:{e.Current}/{e.Max}");
        }
        if (World.HasComponent<Pursuit>(id))
        {
            Pursuit p = World.GetComponent<Pursuit>(id);
            string kind = p.current.GetType().Name;
            string detail = p.current is NavigatePursuit np ? $"({np.goalX},{np.goalY})" : "";
            tags.Add($"Pursuit:{kind}{detail} pri:{p.Priority}");
        }
        if (World.HasComponent<Melee>(id)) tags.Add("Melee");
        if (World.HasComponent<Walkable>(id)) tags.Add("Walkable");
        if (World.HasComponent<Solid>(id)) tags.Add("Solid");
        if (World.HasComponent<Species>(id))
        {
            Species sp = World.GetComponent<Species>(id);
            tags.Add($"Species:{sp.spawn.Method.Name.Replace("Create", "")}");
        }
        if (World.HasComponent<Stats>(id))
        {
            Stats s = World.GetComponent<Stats>(id);
            tags.Add($"Str:{s.Strength} Agi:{s.Agility} Per:{s.Perception}");
        }
        if (World.HasComponent<AgilityPaced>(id))
        {
            AgilityPaced ap = World.GetComponent<AgilityPaced>(id);
            tags.Add($"AgilityPaced next:{ap.NextActTick}");
        }
        if (World.HasComponent<FixedPaced>(id))
        {
            FixedPaced fp = World.GetComponent<FixedPaced>(id);
            tags.Add($"FixedPaced(period:{fp.period}) next:{fp.NextActTick}");
        }
        if (World.HasComponent<Grappled>(id))
        {
            Grappled gp = World.GetComponent<Grappled>(id);
            tags.Add($"Grappled(by:{gp.attackerId})");
        }
        if (World.HasComponent<Predator>(id))
        {
            Predator pr = World.GetComponent<Predator>(id);
            string preyNames = string.Join(",", pr.preySpecies.Select(f => f.Method.Name.Replace("Create", "")));
            tags.Add($"Predator:[{preyNames}]");
        }
        if (World.HasComponent<Corpse>(id)) tags.Add("Corpse");
        if (World.HasComponent<Diet>(id))
        {
            Diet diet = World.GetComponent<Diet>(id);
            tags.Add($"Diet:[{string.Join(",", diet.allowed.Select(k => k.name))}]");
        }
        if (World.HasComponent<Yields>(id))
        {
            Yields yields = World.GetComponent<Yields>(id);
            string parts = string.Join(",", yields.entries.Select(e => $"{e.category.name}x{e.amount}"));
            tags.Add($"Yields:[{parts}]");
        }
        if (World.HasComponent<Collects>(id))
        {
            Collects c = World.GetComponent<Collects>(id);
            tags.Add($"Collects:[{string.Join(",", c.allowed.Select(k => k.name))}]");
        }
        if (World.HasComponent<Container>(id))
        {
            Container c = World.GetComponent<Container>(id);
            string kind = World.HasComponent<Camp>(id) ? "Storage" : "Pack";
            string contents = c.stacks.Count == 0
                ? "empty"
                : string.Join(",", c.stacks.Select(s => $"{s.category.name}x{s.amount}"));
            tags.Add($"{kind}:[{contents}] ({c.Used}/{c.capacity})");
        }
        if (World.HasComponent<Home>(id))
        {
            Home h = World.GetComponent<Home>(id);
            tags.Add($"Home:camp#{h.campId}");
        }
        if (World.HasComponent<Breeding>(id))
        {
            Breeding b = World.GetComponent<Breeding>(id);
            int since = World.tickCount - b.lastBreedTick;
            tags.Add($"Breed:{since}/{b.breedCooldown}");
        }
        if (World.HasComponent<Vegetation>(id))
        {
            Vegetation v = World.GetComponent<Vegetation>(id);
            tags.Add($"Veg:spread{v.spreadChance}");
        }

        Console.WriteLine($"  Entity {id}: {string.Join(", ", tags)}");
    }
}
