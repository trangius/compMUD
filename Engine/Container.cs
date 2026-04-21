namespace Engine;

// ----------------------------------------------------------------------------
// State: what resource kinds this entity will pick up when butchering,
// harvesting, or chopping. Different from Diet — Diet is "what I'll eat",
// Collects is "what I'll carry home". A hunter eats meat but picks up both
// meat and pelts off a kill. Without the split, Diet would have to pretend
// a hunter eats pelts so the butcher step extracts them, and then eating
// from the stockpile would turn pelts into energy. Ugly and wrong as soon
// as pelts get a real use.
// ----------------------------------------------------------------------------
public class Collects
{
    public HashSet<ResourceCategory> allowed;

    public Collects(params ResourceCategory[] kinds)
    {
        allowed = new HashSet<ResourceCategory>(kinds);
    }

    public bool Accepts(ResourceCategory cat) => allowed.Contains(cat);
}

// ----------------------------------------------------------------------------
// A stack of items inside a Container. Same shape as Yield but different
// meaning: a Yield is latent (doesn't exist until drained from a source),
// an ItemStack is real — already extracted, sitting inside a vessel.
// ----------------------------------------------------------------------------
public class ItemStack
{
    public ResourceCategory category;
    public int amount;

    public ItemStack(ResourceCategory category, int amount)
    {
        this.category = category;
        this.amount = amount;
    }
}

// ----------------------------------------------------------------------------
// State: a vessel that holds items. Unlike Yields (latent — produced when
// something processes the source), a Container's items already exist: meat
// already butchered, wood already chopped. Backpack on a hunter, stockpile
// at a camp, a future wagon or chest all use this.
//
// Capacity is one number. Each stack's amount counts against it one-for-one
// for now — 500 meat fills 500 capacity, 80 bones fills 80. Later we can
// scale per category (one log takes more room than ten meat); not yet.
// ----------------------------------------------------------------------------
public class Container
{
    public List<ItemStack> stacks;
    public int capacity;

    public Container(int capacity)
    {
        this.capacity = capacity;
        this.stacks = new List<ItemStack>();
    }

    // ----------------------------------------------------------------------------
    // Total held across all stacks.
    // ----------------------------------------------------------------------------
    public int Used
    {
        get
        {
            int total = 0;
            foreach (ItemStack s in stacks) total += s.amount;
            return total;
        }
    }

    // ----------------------------------------------------------------------------
    // Room left for more items.
    // ----------------------------------------------------------------------------
    public int Free => capacity - Used;

    // ----------------------------------------------------------------------------
    // How much of this category is currently held. Zero if none.
    // ----------------------------------------------------------------------------
    public int CountOf(ResourceCategory cat)
    {
        foreach (ItemStack s in stacks)
            if (s.category == cat) return s.amount;
        return 0;
    }

    // ----------------------------------------------------------------------------
    // Add up to n of the given category. Capped by Free. Merges into the
    // existing stack for that category if there is one; otherwise starts one.
    // Returns how much was actually added.
    // ----------------------------------------------------------------------------
    public int Add(ResourceCategory cat, int n)
    {
        int put = Math.Min(n, Free);
        if (put <= 0) return 0;

        foreach (ItemStack s in stacks)
        {
            if (s.category == cat) { s.amount += put; return put; }
        }
        stacks.Add(new ItemStack(cat, put));
        return put;
    }

    // ----------------------------------------------------------------------------
    // Take up to n from the given category's stack. Returns how much was taken.
    // Removes the stack entirely if it hits zero so an empty Container has
    // zero stacks.
    // ----------------------------------------------------------------------------
    public int Take(ResourceCategory cat, int n)
    {
        foreach (ItemStack s in stacks)
        {
            if (s.category != cat) continue;
            int taken = Math.Min(n, s.amount);
            s.amount -= taken;
            if (s.amount <= 0) stacks.Remove(s);
            return taken;
        }
        return 0;
    }
}
