# Gui frontend

*(Placeholder — expand this when the GUI work picks up.)*

MonoGame graphical frontend for [`Engine/`](../Engine). Same engine as the Console; different
presentation layer.

## Running

```bash
dotnet run --project Gui
```

## Still to document

- Renderer architecture (how sprites map to cells, layer ordering).
- Input handling (keyboard / mouse → engine commands).
- Update/tick cadence (decoupling frame rate from simulation rate).
- Sprite assets and their loading path.
- Camera / viewport behavior.
