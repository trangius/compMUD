# Claude Project Instructions

Imperative rules — always obey. Design reference material lives in `docs/`;
`README.md` is the index. Read the relevant doc when the rule mentions a
concept you don't already have in context.

## Logic direction: check for the positive
Never write logic that enumerates things that block or prevent an action.
Check for the thing that permits it. Movement checks "is there `Walkable`
ground?" — not "is there `Solid`? is there `Liquid`? is there `Lava`?" The
negative list grows forever. The positive check stays one line.

## Architecture: composition, not inheritance
Prefer composition and small interfaces over deep inheritance hierarchies.
Interfaces describe what something *can do*, not what it *is*. See
[docs/engine.composition.md](docs/engine.composition.md) for the full
design rules (components carry behavior, one action per tick, no data-only
classes, etc.) — they're non-negotiable.

```csharp
// Preferred
class Player { public IMovement movement; public IAttack attack; }

// Avoid
class Player : Character : Entity : GameObject ...
```

## The five buckets
Every new concept fits exactly one of: **Entity**, **State**, **Behavior**,
**Effect**, **Category**. Don't smoosh. Decision tree and patterns in
[docs/engine.five-buckets.md](docs/engine.five-buckets.md).

## Adding a new entity
Follow the walkthrough in [docs/engine.add-entity.md](docs/engine.add-entity.md).

## Running and testing
Use the Console frontend — see [docs/console.readme.md](docs/console.readme.md).
You can pipe input for scripted runs.

## Code style
- **Fields are camelCase regardless of visibility.** This differs from
  Microsoft's standard, which uses PascalCase for public fields. Our
  convention makes the call site informative: `obj.X` (PascalCase) = property,
  may have logic; `obj.breedCooldown` (camelCase) = plain data field.
- **PascalCase for properties, types, methods, interfaces.** Standard.
- Prefer explicit types over `var` unless the type is obvious from context.

## General principles
- Characters use circle collision; walls and tiles use rectangle collision.
- Do not refactor large systems unless explicitly asked.

## Comments
- Comments are navigation — you should understand a file by reading only the
  comments.
- **Never delete comments without asking.** If wrong, update it — don't remove it.
- Add a comment before every code block of 4+ lines.
- For functions, use a visual separator. These matter — the user has poor
  eyesight and relies on them for scanning and for seeing structure in the
  minimap. Don't remove them even if they feel redundant.
  ```
  // ----------------------------------------------------------------------------
  // <what this function does>
  // ----------------------------------------------------------------------------
  ```
- Language: short, concrete, narrating. Like telling someone what happens next.
  - Good: *"Find the nearest food and move toward it"*
  - Good: *"Both parents go on cooldown after mating"*
  - Bad: *"This method searches for the nearest food entity within vision range"*
  - Bad: *"Execute harvesting behavior on the target Drops entity"*
- Flag gotchas — things that would trip someone up or break if changed.
- For types in the five buckets, prefix the class comment with the bucket
  name: `// State: ...`, `// Behavior: ...`, `// Effect: ...`, `// Category: ...`.
  Makes taxonomy visible at a glance without adding interface plumbing.

## Git
When asked to suggest a commit message: check `git diff` first, then suggest a
non-complex, one-line, straightforward, non-jargon commit message.

Never run `git commit` yourself unless explicitly told to (e.g. "commit it",
"go ahead and commit"). "We should commit" is discussion, not an instruction.
The user does the commits.

## Updating docs

Docs in `docs/` describe concepts — how the engine is put together, how a
tick runs, what the buckets are, how breeding or movement or species
identity works. They do not quote tuning numbers (stat values, costs,
cooldowns, probabilities, pool sizes). Anything that gets tuned lives in
the source, not in prose.

That means most code changes do not drift the docs. Tuning a stat,
adjusting a cooldown, changing a drain rate — the code changes and the
docs still read correctly, because they talk about the mechanism, not the
number. Don't proactively update docs for that kind of change.

What DOES drift the docs: renaming a class, method, field, or file the
docs reference by name. When you rename, the docs suddenly describe code
that no longer exists.

**One mandatory check.** After any rename, run `grep -r "OldName" docs/`.
Every hit is a doc describing code that doesn't exist. Fix in the same
change. That's the whole default discipline.

**Larger doc updates happen on explicit request.** When the user asks for
a doc refresh, a reading pass, or a rewrite, the table below says what
each doc covers so you can pick the right one. Don't rewrite docs for a
refactor unless the refactor changed the design, not just the
implementation.

| Doc | Covers |
|---|---|
| `docs/projects.md` | The three .NET projects and the frontend/engine contract. |
| `docs/console.readme.md` | The Console frontend — commands, sprite legend, scripted-run pattern. |
| `docs/gui.readme.md` | The MonoGame frontend (placeholder). |
| `docs/engine.composition.md` | The composition model — integer ids, components-with-methods, the rules. |
| `docs/engine.five-buckets.md` | The Entity / State / Behavior / Effect / Category taxonomy. |
| `docs/engine.tick.md` | The two-pass dispatcher (actions then effects), grapple handling, cost multipliers. |
| `docs/engine.scheduler.md` | `AgilityPaced` vs `FixedPaced`, action-cost mechanics, baby-first-period. |
| `docs/engine.stats.md` | Stats schema, derived abilities, stat-vs-resource split. |
| `docs/engine.movement.md` | 8-connected movement helpers, BFS, vision-vs-reachability. |
| `docs/engine.species.md` | Species identity via the spawn delegate; breeding, hunting, caps. |
| `docs/engine.spatial-index.md` | Position's self-sync pattern, the four write paths, the read-only query surface. |
| `docs/engine.filestructure.md` | Folder and file map of `Engine/`; file-per-feature principle. |
| `docs/engine.add-entity.md` | Recipe for adding a new creature — bucket decisions, hawk example. |
| `docs/engine.examplerun.md` | Call-stack trace of startup, a quiet tick, a bite-with-grapple, and a breeding tick. |
| `README.md` | Entry doc — the "how the engine is built" overview plus the doc index. |
| `CLAUDE.md` (this file) | Imperative rules for working in this repo. |
