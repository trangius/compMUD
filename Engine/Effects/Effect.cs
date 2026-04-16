namespace Engine;

// ----------------------------------------------------------------------------
// Passive per-tick updates. Unlike behaviors (pick one via priority), every
// effect on an entity runs every tick. Use for draining/decay/regen/status
// effects — any "happens to me automatically" process that shouldn't compete
// with active actions.
// ----------------------------------------------------------------------------

public interface IEffect
{
    void Apply(int entityId);
}

// An entity's passive effects. All of them run every tick, in list order.
public class Effects
{
    public List<IEffect> list;

    public Effects(params IEffect[] effects)
    {
        list = new List<IEffect>(effects);
    }
}
