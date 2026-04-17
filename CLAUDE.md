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
When you change code, check whether any doc in `docs/` becomes inaccurate.
The `README.md` index lists an **"Update when: ..."** trigger next to each
doc. Match the change you're making against those triggers; update the
relevant docs in the same change.

Don't wait to be asked. Stale docs rot fast.
