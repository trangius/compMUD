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

    // Sidebar mode — game panel shown by default, backtick toggles to debug
    private bool showDebugSidebar = false;

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

        Window.Title = $"Explorer — {ticksPerSecond} ticks/s";
    }

    // ------------------------------------------------------------------------
    // Update — handle input and advance the simulation
    // ------------------------------------------------------------------------
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyState = Keyboard.GetState();

        if (keyState.IsKeyDown(Keys.Escape)) Exit();

        // Adjust simulation speed with Up/Down arrows
        if (keyState.IsKeyDown(Keys.Up) && !previousKeyState.IsKeyDown(Keys.Up))
        {
            ticksPerSecond = Math.Min(100, ticksPerSecond + 1);
            Window.Title = $"Explorer — {ticksPerSecond} ticks/s";
        }
        if (keyState.IsKeyDown(Keys.Down) && !previousKeyState.IsKeyDown(Keys.Down))
        {
            ticksPerSecond = Math.Max(1, ticksPerSecond - 1);
            Window.Title = $"Explorer — {ticksPerSecond} ticks/s";
        }

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
    // DrawDebugSidebar — stats, population counts, and message log
    // ------------------------------------------------------------------------
    private void DrawDebugSidebar()
    {
        int sidebarX = (World.mapWidth + 1) * cellWidth;
        int line = 0;
        string sep = new string('\u2500', sidebarCharWidth);

        // Title
        font.DrawText(spriteBatch, "DEBUG", new Vector2(sidebarX, line * cellHeight), Color.White);
        line++;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;

        // Stats
        font.DrawText(spriteBatch, $"Turn: {World.tickCount}", new Vector2(sidebarX, line * cellHeight), new Color(200, 200, 150));
        line++;
        font.DrawText(spriteBatch, $"Speed: {ticksPerSecond} ticks/s", new Vector2(sidebarX, line * cellHeight), new Color(150, 150, 150));
        line++;

        // Count populations
        int rabbits = 0, wolves = 0;
        foreach (int id in World.AllWithComponent<Named>())
        {
            string name = World.GetComponent<Named>(id).name;
            if (name == "Rabbit") rabbits++;
            else if (name == "Wolf") wolves++;
        }
        int corpses = World.AllWithComponent<Corpse>().Count;

        font.DrawText(spriteBatch, $"Rabbits: {rabbits}", new Vector2(sidebarX, line * cellHeight), Color.White);
        line++;
        font.DrawText(spriteBatch, $"Wolves:  {wolves}", new Vector2(sidebarX, line * cellHeight), new Color(200, 60, 60));
        line++;
        font.DrawText(spriteBatch, $"Corpses: {corpses}", new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, line * cellHeight), Color.Gray);
        line++;

        // Message log — show recent events
        int maxLogLine = World.mapHeight - 5;
        int logCount = Math.Min(World.messageLog.Count, maxLogLine - line);
        for (int i = 0; i < logCount; i++)
        {
            string msg = World.messageLog[i];
            if (msg.Length > sidebarCharWidth)
                msg = msg.Substring(0, sidebarCharWidth);
            font.DrawText(spriteBatch, msg, new Vector2(sidebarX, line * cellHeight), new Color(180, 180, 180));
            line++;
        }

        // Controls legend at the bottom
        int legendStart = World.mapHeight - 5;
        font.DrawText(spriteBatch, sep, new Vector2(sidebarX, legendStart * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "\u2191\u2193  Speed", new Vector2(sidebarX, (legendStart + 1) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "ESC Quit", new Vector2(sidebarX, (legendStart + 2) * cellHeight), Color.Gray);
        font.DrawText(spriteBatch, "`   Game panel", new Vector2(sidebarX, (legendStart + 3) * cellHeight), Color.Gray);
    }
}
