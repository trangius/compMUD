# Projects

Three .NET projects live in this repo, wired together by `composition.sln`:

## `Engine/`
The simulation. Pure logic — no I/O, no rendering. Exposes `World`, `Archetypes`,
and the five-bucket taxonomy that every concept fits into.

## `Console/`
Text-grid frontend. Drives the engine tick by tick from a terminal with commands
like `look`, `tick`, `info`. Used primarily to test and inspect the engine
without pulling in MonoGame. See [console.readme.md](console.readme.md).

## `Gui/`
MonoGame graphical frontend. Same engine, different presentation. Placeholder
docs for now — see [gui.readme.md](gui.readme.md).

## How they compose

Both frontends reference `Engine/` and call exactly three things:

1. `World.Initialize(width, height)` — set the map dimensions.
2. An area builder like `HomeArea.StartingArea()` — populate terrain, creatures, spawners.
3. `World.Tick()` in a loop — advance simulation one step per call.

The engine doesn't know which frontend is driving it. Adding a new frontend
means linking the Engine project and writing a `Main` that does those three
calls.
