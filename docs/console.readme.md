# Console frontend

Text-grid frontend for `Engine/`. Prints the world as ASCII, reads commands on
stdin, calls `World.Tick()` on demand. Use it to drive the engine tick by tick
and inspect entity state at specific cells.

## Running

```bash
dotnet run --project Console
```

## Commands

| Command | What it does |
|---|---|
| `look` | Render the map. |
| `tick` or `t` | Run one tick and render. |
| `tick <n>` | Run `n` ticks (no render in between), then render. |
| `status` | Counts: rabbits, wolves, corpses, vegetation, turn. |
| `info <x> <y>` | List every entity at `(x, y)` with its components. |
| `log` | Recent engine log messages (deaths, attacks, births, etc.). |
| `quit` or `q` | Exit. |

## Typical debug session

```
> status         # see population at turn 0
> tick 100       # advance 100 ticks
> status         # see what happened
> look           # see where everything ended up
> info 24 2      # inspect the rabbit at that cell — HP, energy, species, pace
> log            # any deaths/kills in the last few ticks?
```

## Sprite legend

- `.` grass (Walkable)
- `#` wall
- `~` water
- `T` tree
- `*` bush
- `r` rabbit
- `W` wolf
- `%` corpse (pink — fresh, still has meat yields)
- `%` bones (gray — meat stripped; pelt and/or bone yields remain)

Rendering picks the top-layer sprite per cell; layers are set on each archetype's `Appearance`.

## When writing engine changes

Run the Console to drive tests. Scripted runs work by piping stdin:

```bash
printf 'status\ntick 500\nstatus\nlog\nquit\n' | dotnet run --project Console
```

This is how most of the ecosystem-balance iteration in this repo was validated.
