using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Engine;
namespace Gui;

// ============================================================================
// Game1 — MonoGame entry point: rendering, input, and tick timing
// ============================================================================

public class Game1 : Game
{
    private GraphicsDeviceManager graphics;
    private SpriteBatch spriteBatch;

    private FontSystem fontSystem;
    private SpriteFontBase font;

    // Smaller font used for the entity-id labels drawn on the map. Sized in
    // LoadContent to roughly a third of the main cell font.
    private SpriteFontBase smallFont;

    // 1×1 white texture — we tint and stretch it to draw the per-creature bars.
    // Creating one pixel once is cheaper than a new RenderTarget per bar.
    private Texture2D pixel;

    // Sprite registry — maps spriteId to MonoGame glyph and color
    // Uses Nerd Font icons from FiraCode Nerd Font Mono
    private Dictionary<string, (string glyph, Color color)> sprites = new()
    {
        ["grass"]   = (",", new Color(60, 120, 40)),
        ["wall"]    = ("\U000f07fe", new Color(140, 140, 140)),
        ["water"]   = ("~", new Color(60, 100, 180)),
        ["tree"]    = ("\ue21c", new Color(0, 120, 0)),
        ["bush"]    = ("\U000f024a", new Color(100, 160, 50)),
        ["rabbit"]  = ("\U000f0907", new Color(220, 220, 220)),
        ["wolf"]    = ("\uedde", new Color(200, 60, 60)),
        ["corpse"]  = ("\U000f068c", new Color(150, 150, 150)),
        ["berries"] = ("\U000f1042", new Color(180, 50, 50)),
    };

    private int ticksPerSecond = 1;
    private double tickTimer = 0;
    private KeyboardState previousKeyState;

    // Key-repeat timers for Up/Down (speed control). On first press we fire
    // immediately; while the key is held we wait `keyRepeatDelay` and then
    // fire every `keyRepeatInterval`. Mirrors a typical OS keyboard repeat.
    private double upRepeatTimer = 0;
    private double downRepeatTimer = 0;
    private const double keyRepeatDelay = 0.35;
    private const double keyRepeatInterval = 0.05;

    // Sidebar mode — debug panel shown by default, backtick toggles to game
    private bool showDebugSidebar = true;

    private int cellWidth;
    private int cellHeight;
    private int computedFontSize;
    private int screenWidth;
    private int screenHeight;

    // Sidebar config — character width of the right panel
    private const int sidebarCharWidth = 30;
    private int totalColumns;

    // ------------------------------------------------------------------------
    // Constructor — fullscreen with hardware mode switch
    // ------------------------------------------------------------------------
    public Game1()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        graphics.IsFullScreen = true;
        graphics.HardwareModeSwitch = true;
    }

    // ------------------------------------------------------------------------
    // Initialize — create the world, generate the map, set up systems
    // ------------------------------------------------------------------------
    protected override void Initialize()
    {
        World.Initialize(80, 50);
        HomeArea.StartingArea();

        totalColumns = World.mapWidth + 1 + sidebarCharWidth;
        previousKeyState = Keyboard.GetState();
        base.Initialize();
    }

    // ------------------------------------------------------------------------
    // LoadContent — set up font system and calculate cell sizes
    // ------------------------------------------------------------------------
    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);

        // One-pixel texture reused by every bar draw below — stretched to a rect
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        // Request native retina resolution for crisp rendering
        int displayWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        int displayHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        graphics.PreferredBackBufferWidth = displayWidth * 2;
        graphics.PreferredBackBufferHeight = displayHeight * 2;
        graphics.ApplyChanges();

        // Read back the actual pixel dimensions
        screenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
        screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

        // Load the monospace nerd font with a larger atlas for hi-dpi
        FontSystemSettings fontSettings = new FontSystemSettings
        {
            TextureWidth = 4096,
            TextureHeight = 4096,
            GlyphRenderResult = GlyphRenderResult.NonPremultiplied
        };
        fontSystem = new FontSystem(fontSettings);
        string fontPath = Path.Combine(Content.RootDirectory, "FiraCodeNerdFontMono-Regular.ttf");
        fontSystem.AddFont(File.ReadAllBytes(fontPath));

        // Find the largest font that fits grid + sidebar at native resolution
        int fitSize = 8;
        for (int testSize = 8; testSize <= 120; testSize++)
        {
            SpriteFontBase testFont = fontSystem.GetFont(testSize);
            Vector2 charSize = testFont.MeasureString("M");
            int w = (int)Math.Ceiling(charSize.X);
            int h = (int)Math.Ceiling(charSize.Y);
            if (w * totalColumns <= screenWidth && h * World.mapHeight <= screenHeight)
                fitSize = testSize;
            else
                break;
        }
        computedFontSize = fitSize;

        font = fontSystem.GetFont(computedFontSize);
        Vector2 finalSize = font.MeasureString("M");
        cellWidth = (int)Math.Ceiling(finalSize.X);
        cellHeight = (int)Math.Ceiling(finalSize.Y);

        // Small font for the id labels drawn on top of each creature's cell
        int smallFontSize = Math.Max(8, computedFontSize / 3);
        smallFont = fontSystem.GetFont(smallFontSize);

        Window.Title = $"Explorer — {ticksPerSecond} ticks/s";
    }

    // ------------------------------------------------------------------------
    // Update — handle input and advance the simulation
    // ------------------------------------------------------------------------
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyState = Keyboard.GetState();

        if (keyState.IsKeyDown(Keys.Escape)) Exit();

        // Adjust simulation speed — Up/Down auto-repeat while held
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        HandleSpeedKey(keyState, previousKeyState, Keys.Up, dt, ref upRepeatTimer, +1);
        HandleSpeedKey(keyState, previousKeyState, Keys.Down, dt, ref downRepeatTimer, -1);

        // Toggle sidebar mode with backtick
        if (keyState.IsKeyDown(Keys.OemTilde) && !previousKeyState.IsKeyDown(Keys.OemTilde))
        {
            showDebugSidebar = !showDebugSidebar;
        }

        previousKeyState = keyState;

        // Accumulate time and run ticks at the configured rate
        tickTimer += gameTime.ElapsedGameTime.TotalSeconds;
        double tickInterval = 1.0 / ticksPerSecond;
        while (tickTimer >= tickInterval)
        {
            World.Tick();
            tickTimer -= tickInterval;
        }

        base.Update(gameTime);
    }

    // ------------------------------------------------------------------------
    // HandleSpeedKey — fire the speed change on the initial keypress, then on
    // every keyRepeatInterval after keyRepeatDelay has elapsed. The timer IS
    // the state: on edge we seed it to the initial delay; while held we count
    // down and re-seed to the repeat interval after each fire.
    // ------------------------------------------------------------------------
    private void HandleSpeedKey(KeyboardState now, KeyboardState prev, Keys key, double dt, ref double timer, int delta)
    {
        if (!now.IsKeyDown(key))
        {
            timer = 0;
            return;
        }

        // Edge: fire once and start the initial delay before auto-repeat
        if (!prev.IsKeyDown(key))
        {
            ChangeTicksPerSecond(delta);
            timer = keyRepeatDelay;
            return;
        }

        // Held: count down, fire when the timer elapses, then reseed for repeat
        timer -= dt;
        if (timer <= 0)
        {
            ChangeTicksPerSecond(delta);
            timer = keyRepeatInterval;
        }
    }

    // ------------------------------------------------------------------------
    // ChangeTicksPerSecond — bump the tick rate by delta, clamp, update title.
    // ------------------------------------------------------------------------
    private void ChangeTicksPerSecond(int delta)
    {
        ticksPerSecond = Math.Clamp(ticksPerSecond + delta, 1, 100);
        Window.Title = $"Explorer — {ticksPerSecond} ticks/s";
    }

    // ------------------------------------------------------------------------
    // Draw — render grid area and sidebar
    // ------------------------------------------------------------------------
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            null, null, null, null);

        DrawGrid();
        DrawCreatureIds();
        DrawSidebar();

        spriteBatch.End();
        base.Draw(gameTime);
    }

    // ------------------------------------------------------------------------
    // DrawGrid — render every map cell, showing the highest-layer entity
    // ------------------------------------------------------------------------
    private void DrawGrid()
    {
        for (int x = 0; x < World.mapWidth; x++)
        {
            for (int y = 0; y < World.mapHeight; y++)
            {
                List<int> atCell = World.EntitiesAt(x, y);

                // Find the entity with the highest render layer
                string glyph = " ";
                Color color = Color.White;
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

                Vector2 position = new Vector2(x * cellWidth, y * cellHeight);
                font.DrawText(spriteBatch, glyph, position, color);
            }
        }
    }

    // ------------------------------------------------------------------------
    // DrawCreatureIds — small entity-id label at the top-left of every living
    // creature's cell. The id cross-references the sidebar's creature list so
    // you can match a row to the dot on the map.
    // ------------------------------------------------------------------------
    private void DrawCreatureIds()
    {
        foreach (int id in World.AllWithComponent<Health>())
        {
            if (!World.HasComponent<Position>(id)) continue;
            Position pos = World.GetComponent<Position>(id);
            Vector2 textPos = new Vector2(pos.X * cellWidth + 1, pos.Y * cellHeight);
            smallFont.DrawText(spriteBatch, id.ToString(), textPos, Color.White);
        }
    }

    // ------------------------------------------------------------------------
    // DrawBarRect — filled colored rectangle with a dim backdrop so the empty
    // portion of the bar is still visible. Used by the sidebar creature list.
    // ------------------------------------------------------------------------
    private void DrawBarRect(int x, int y, int w, int h, float fraction, Color color)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        spriteBatch.Draw(pixel, new Rectangle(x, y, w, h), new Color(40, 40, 40));
        int filled = (int)(w * fraction);
        if (filled < 1) return;
        spriteBatch.Draw(pixel, new Rectangle(x, y, filled, h), color);
    }

    // ------------------------------------------------------------------------
    // DrawSidebar — dispatch to the active sidebar panel
    // ------------------------------------------------------------------------
    private void DrawSidebar()
    {
        if (showDebugSidebar)
            DrawDebugSidebar();
        else
            DrawGameSidebar();
    }

    // ------------------------------------------------------------------------
    // DrawGameSidebar — main game info panel (default)
    // ------------------------------------------------------------------------
    private void DrawGameSidebar()
    {
        int sidebarX = (World.mapWidth + 1) * cellWidth;
        int line = 0;
        string sep = new string('\u2500', sidebarCharWidth);

        // Title
        font.DrawText(spriteBatch, "EXPLORER", new Vector2(sidebarX, line * cellHeight), Color.White);
        line++;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;

        // Controls legend at the bottom
        int legendStart = World.mapHeight - 5;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, legendStart * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "\u2191\u2193  Speed", new Vector2(sidebarX, (legendStart + 1) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "ESC Quit", new Vector2(sidebarX, (legendStart + 2) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "`   Debug panel", new Vector2(sidebarX, (legendStart + 3) * cellHeight), Color.Gray);
    }

    // ------------------------------------------------------------------------
    // DrawDebugSidebar — header stats, per-creature rows with big rect bars,
    // and a message log tail. Row format:
    //   "<glyph> <id>" at the left (6 chars), then four bars side by side:
    //   HP (red) | Energy (blue) | Breed (green) | Grappled indicator (orange).
    // Bar columns are fixed width so the map stays readable at a glance.
    // ------------------------------------------------------------------------
    private void DrawDebugSidebar()
    {
        int sidebarX = (World.mapWidth + 1) * cellWidth;
        int line = 0;
        string sep = new string('\u2500', sidebarCharWidth);

        // Header — turn, speed, species counts
        font.DrawText(spriteBatch, "DEBUG", new Vector2(sidebarX, line * cellHeight), Color.White);
        line++;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;
        font.DrawText(spriteBatch, $"Turn: {World.tickCount}", new Vector2(sidebarX, line * cellHeight), new Color(200, 200, 150));
        line++;
        font.DrawText(spriteBatch, $"Speed: {ticksPerSecond} ticks/s", new Vector2(sidebarX, line * cellHeight), new Color(150, 150, 150));
        line++;

        int rabbits = 0, wolves = 0;
        foreach (int id in World.AllWithComponent<Named>())
        {
            string name = World.GetComponent<Named>(id).name;
            if (name == "Rabbit") rabbits++;
            else if (name == "Wolf") wolves++;
        }
        int corpses = World.AllWithComponent<Corpse>().Count;
        font.DrawText(spriteBatch, $"R:{rabbits} W:{wolves} C:{corpses}", new Vector2(sidebarX, line * cellHeight), Color.White);
        line++;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;

        // Creature-row geometry — label then three fixed bar columns
        int labelChars = 6;
        int barCols = 7;                         // chars per bar column
        int barColWidth = barCols * cellWidth;
        int barGap = 2;
        int barsStartX = sidebarX + labelChars * cellWidth;
        int barH = cellHeight * 3 / 5;           // bar sits centered in the row

        // Column headers — each label drawn in its bar's own color so the
        // color→meaning mapping is visible above the rows that use it.
        font.DrawText(spriteBatch, "HP", new Vector2(barsStartX, line * cellHeight), Color.Red);
        font.DrawText(spriteBatch, "Energy", new Vector2(barsStartX + barColWidth, line * cellHeight), Color.Blue);
        font.DrawText(spriteBatch, "Breed", new Vector2(barsStartX + 2 * barColWidth, line * cellHeight), Color.Green);
        font.DrawText(spriteBatch, "Grap", new Vector2(barsStartX + 3 * barColWidth, line * cellHeight), Color.Orange);
        line++;

        // Budget the rest of the sidebar: keep rows for log + controls at bottom
        int bottomReserved = 7;                  // separator + log lines + controls
        int maxCreatureLine = World.mapHeight - bottomReserved;

        foreach (int id in World.AllWithComponent<Health>())
        {
            if (line >= maxCreatureLine) break;

            string glyph = "?";
            Color rowColor = Color.White;
            if (World.HasComponent<Appearance>(id))
            {
                Appearance app = World.GetComponent<Appearance>(id);
                if (sprites.ContainsKey(app.spriteId))
                {
                    glyph = sprites[app.spriteId].glyph;
                    rowColor = sprites[app.spriteId].color;
                }
            }

            int y = line * cellHeight;
            int barY = y + (cellHeight - barH) / 2;

            // Glyph + id at left. Padded so ids of different widths align.
            font.DrawText(spriteBatch, $"{glyph} {id,3}", new Vector2(sidebarX, y), rowColor);

            // HP — always present (every row has Health, by selection above)
            Health hp = World.GetComponent<Health>(id);
            DrawBarRect(barsStartX, barY, barColWidth - barGap, barH,
                (float)hp.Current / hp.Max, Color.Red);

            // Energy — present on anything that can starve
            if (World.HasComponent<Energy>(id))
            {
                Energy en = World.GetComponent<Energy>(id);
                DrawBarRect(barsStartX + barColWidth, barY, barColWidth - barGap, barH,
                    (float)en.Current / en.Max, Color.Blue);
            }

            // Breed readiness — fills as cooldown expires; full = ready to mate
            if (World.HasComponent<Breeding>(id))
            {
                Breeding br = World.GetComponent<Breeding>(id);
                int since = World.tickCount - br.lastBreedTick;
                float readiness = Math.Clamp((float)since / br.breedCooldown, 0f, 1f);
                DrawBarRect(barsStartX + 2 * barColWidth, barY, barColWidth - barGap, barH,
                    readiness, Color.Green);
            }

            // Grappled — full orange square in the rightmost slot when pinned
            if (World.HasComponent<Grappled>(id))
            {
                int grapX = barsStartX + 3 * barColWidth;
                DrawBarRect(grapX, barY, 2 * cellWidth, barH, 1f, Color.Orange);
            }

            line++;
        }

        // Separator before the message log
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;

        // Message log — fills whatever rows are left above the controls
        int legendStart = World.mapHeight - 5;
        int logCount = Math.Max(0, legendStart - line);
        logCount = Math.Min(World.messageLog.Count, logCount);
        for (int i = 0; i < logCount; i++)
        {
            string msg = World.messageLog[i];
            if (msg.Length > sidebarCharWidth)
                msg = msg.Substring(0, sidebarCharWidth);
            font.DrawText(spriteBatch, msg, new Vector2(sidebarX, line * cellHeight), new Color(180, 180, 180));
            line++;
        }

        // Controls legend pinned at the bottom
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, legendStart * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "\u2191\u2193  Speed", new Vector2(sidebarX, (legendStart + 1) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "ESC Quit", new Vector2(sidebarX, (legendStart + 2) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "`   Game panel", new Vector2(sidebarX, (legendStart + 3) * cellHeight), Color.Gray);
    }
}
