using Engine;

// Sprite registry — maps spriteId to console glyph and color
Dictionary<string, (string glyph, ConsoleColor color)> sprites = new()
{
    ["grass"]   = (".", ConsoleColor.Green),
    ["wall"]    = ("#", ConsoleColor.Gray),
    ["water"]   = ("~", ConsoleColor.Blue),
    ["tree"]    = ("T", ConsoleColor.DarkGreen),
    ["bush"]    = ("*", ConsoleColor.DarkYellow),
    ["rabbit"]  = ("r", ConsoleColor.White),
    ["wolf"]    = ("W", ConsoleColor.Red),
    ["corpse"]  = ("%", ConsoleColor.Gray),
    ["berries"] = ("f", ConsoleColor.DarkYellow),
};

World.Initialize(60, 30);
HomeArea.StartingArea();

Console.WriteLine($"Map generated: {World.mapWidth}x{World.mapHeight}, {World.EntityCount} entities");
Console.WriteLine("Commands: look, tick [n], status, log, info <x> <y>, quit");
Console.WriteLine();

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
    else
    {
        Console.WriteLine("Unknown command. Try: look, tick [n], status, log, info <x> <y>, quit");
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
    int rabbits = 0, wolves = 0;

    foreach (int id in World.AllWithComponent<Named>())
    {
        string name = World.GetComponent<Named>(id).name;
        if (name == "Rabbit") rabbits++;
        else if (name == "Wolf") wolves++;
    }

    int corpses = World.AllWithComponent<Corpse>().Count;
    int vegetation = World.AllWithComponent<Vegetation>().Count;

    Console.WriteLine($"Turn {World.tickCount} — Rabbits: {rabbits}  Wolves: {wolves}  Corpses: {corpses}  Vegetation: {vegetation}");
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
        if (World.HasComponent<Attacking>(id)) tags.Add($"Atk:{World.GetComponent<Attacking>(id).Damage}");
        if (World.HasComponent<Walkable>(id)) tags.Add("Walkable");
        if (World.HasComponent<Solid>(id)) tags.Add("Solid");
        if (World.HasComponent<Species>(id))
        {
            Species sp = World.GetComponent<Species>(id);
            tags.Add($"Species:{sp.spawn.Method.Name.Replace("Create", "")}");
        }
        if (World.HasComponent<Scheduler>(id))
        {
            Scheduler sch = World.GetComponent<Scheduler>(id);
            tags.Add($"Pace:{sch.period} next:{sch.nextActTick}");
        }
        if (World.HasComponent<Predator>(id))
        {
            Predator pr = World.GetComponent<Predator>(id);
            string preyNames = string.Join(",", pr.hunts.Select(f => f.Method.Name.Replace("Create", "")));
            tags.Add($"Predator:[{preyNames}]");
        }
        if (World.HasComponent<Corpse>(id)) tags.Add("Corpse");
        if (World.HasComponent<Diet>(id))
        {
            Diet diet = World.GetComponent<Diet>(id);
            tags.Add($"Diet:[{string.Join(",", diet.allowed.Select(k => k.name))}]");
        }
        if (World.HasComponent<Drops>(id))
        {
            Drops drops = World.GetComponent<Drops>(id);
            tags.Add($"Drops:{drops.resourceType.name}x{drops.amount}");
        }
        if (World.HasComponent<ResourceItem>(id))
        {
            ResourceItem item = World.GetComponent<ResourceItem>(id);
            tags.Add($"Res:{item.resourceType.name}x{item.amount}");
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
