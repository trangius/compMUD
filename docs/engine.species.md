# Species identity

**An entity's species IS its archetype spawn delegate.** Two entities are the
same species if-and-only-if their `Species.spawn` points at the same method
(e.g. both point at `Archetypes.CreateRabbit`). Comparison is reference
equality (pointer equality).

No enum. No string tag. No species registry. The method itself is the ID.

## The component

```csharp
public class Species
{
    public required Func<int, int, int> spawn;   // archetype's Create method — the ID

    public static int CountAll(Func<int,int,int> speciesSpawn);
    public static int CountInRadius(int cx, int cy, int radius,
                                    Func<int,int,int> speciesSpawn,
                                    int excludeId = -1);
}
```

## Why the delegate

- The archetype's `Create` method exists anyway — it's how babies are built.
  Reusing it as the identity is zero extra infrastructure.
- Adding a new species = adding a `CreateX` method + a `Species { spawn = CreateX }`
  line in the archetype. Nothing else, nowhere else.
- Delegate equality in C# compares `MethodInfo`, so two different
  `Func<int,int,int>` instances both pointing at `CreateRabbit` compare
  equal. `HashSet<Func<...>>` works correctly.

## Two shapes of check

**Breeding — equality.** "Are you the same species as me?"
```csharp
if (otherSpecies.spawn == mySpecies.spawn)  // same archetype → same species
```
Used in `BreedBehavior.FindAdjacentMate`.

**Predation — set membership.** "Are you on my hunt list?"
```csharp
public class Predator
{
    public HashSet<Func<int,int,int>> hunts;   // e.g. { CreateRabbit, CreateHare }
    public bool Hunts(Func<int,int,int> speciesSpawn) => hunts.Contains(speciesSpawn);
}
```
Wolf's `new Predator(CreateRabbit)` gives it a hunt set of one species. Same
shape as `Diet.Accepts(resource)` — set membership.

## Where species identity shows up

- **`Species` component** on an entity declares which species it is.
- **`Breeding.FindAdjacentMate`** — match same-species neighbors.
- **`Breeding.globalCap`** — count world-wide same-species via `Species.CountAll`.
- **`Vegetation.HasRoom`** — count local same-species via `Species.CountInRadius`.
- **`Predator.hunts`** — which species this entity hunts.
- **`FleeBehavior`** — prey finds a predator whose `hunts` contains its own
  species.

Every place that asks "who's my kind?" or "who do I care about?" goes through
`Species.spawn`.

## How to add a predator-prey pair

Example — adding hawks that hunt rabbits AND mice:

1. Add `Archetypes.CreateMouse` (the new prey species).
2. Add `Archetypes.CreateHawk` — attach `new Species { spawn = CreateHawk }`
   and `new Predator(CreateRabbit, CreateMouse)`.
3. Done. No enum to update, no registry. Hawks now hunt rabbits and mice
   specifically; wolves (whose `Predator.hunts` only contains `CreateRabbit`)
   still only hunt rabbits.

Rabbits flee from anything whose `Predator.hunts` contains `CreateRabbit` —
so they'll flee from both wolves and hawks without any extra wiring.

## Gotcha

Never compare species by name (`named.name`). Names are display strings and
can collide or drift. Always compare by `Species.spawn` reference.
