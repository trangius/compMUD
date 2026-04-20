namespace Engine;

// ----------------------------------------------------------------------------
// Category: a kind of resource (meat, berry, pelt, wood, ...). Real object,
// not a type tag — each built-in kind is a singleton in the Resources registry
// below. Identity is the object reference (pointer equality); the name is for
// display only. Plugins extend by declaring their own static readonly
// ResourceCategory somewhere and using it at call sites — no engine change.
// ----------------------------------------------------------------------------
public class ResourceCategory
{
    public readonly string name;  // singleton label; readonly so Resources.Meat.name can never drift

    public ResourceCategory(string name)
    {
        this.name = name;
    }
}

// Built-in resource kinds. Each static readonly field is a singleton instance.
public static class Resources
{
    public static readonly ResourceCategory Meat = new("meat");
    public static readonly ResourceCategory Berry = new("berry");
    public static readonly ResourceCategory Pelt = new("pelt");
    public static readonly ResourceCategory Bone = new("bone");
}

// ----------------------------------------------------------------------------
// A single line of a Yields declaration — a category plus how much is left.
// Not an entity, not a component; just the shape each Yields entry has.
// ----------------------------------------------------------------------------
public class Yield
{
    public ResourceCategory category;
    public int amount;

    public Yield(ResourceCategory category, int amount)
    {
        this.category = category;
        this.amount = amount;
    }
}

// ----------------------------------------------------------------------------
// State: what this entity produces when processed — eaten, grazed, butchered,
// chopped. A bush yields berries; a rabbit corpse yields meat, pelt, bones; a
// tree yields wood. Yields are *latent*: items don't exist until someone
// drains them. Drain(cat, n) reduces that category's amount; when it hits zero
// the entry is removed, so an empty Yields has zero entries.
// ----------------------------------------------------------------------------
public class Yields
{
    public List<Yield> entries;

    public Yields(params Yield[] initial)
    {
        entries = new List<Yield>(initial);
    }

    // ----------------------------------------------------------------------------
    // Return the yield for this category, or null if there isn't one.
    // ----------------------------------------------------------------------------
    public Yield? Get(ResourceCategory cat)
    {
        foreach (Yield y in entries)
            if (y.category == cat) return y;
        return null;
    }

    // ----------------------------------------------------------------------------
    // Take up to n from the given category's yield. Returns how much was taken.
    // Removes the entry entirely if it hits zero.
    // ----------------------------------------------------------------------------
    public int Drain(ResourceCategory cat, int n)
    {
        Yield? y = Get(cat);
        if (y == null) return 0;
        int taken = Math.Min(n, y.amount);
        y.amount -= taken;
        if (y.amount <= 0) entries.Remove(y);
        return taken;
    }
}
