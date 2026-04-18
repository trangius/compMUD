namespace Engine;

// State: basic stats of a living being. Shared schema — every creature with
// `Stats` has every field. Deliberately binary: entities either have stats
// (rabbits, wolves, future humans) or they don't (trees, bushes, walls).
//
// Not a place for resources (Health, Energy) — those are stateful and have
// their own components. Stats are static base values that drive derived
// abilities via StatMath (bite damage, vision range, action period, etc.).
//
// Scale convention: 1–100 (Dark-Souls style). Defaults at 1 so a new field
// here doesn't require touching every archetype.
public class Stats
{
    public int Strength = 1;
    public int Agility = 1;
    public int Perception = 1;
    // Future fields land here with a default: Toughness, Mass, Intelligence,
    // Charisma, ...
}
