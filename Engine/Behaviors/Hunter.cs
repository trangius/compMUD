namespace Engine;

// State: this entity belongs to a camp. The id points at the camp entity
// the hunter returns to between trips. Pure data, but it's the whole reason
// the hunter has a goal loop at all — without a home there's no "back to
// base" concept.
public class Home
{
    public int campId;

    public Home(int campId)
    {
        this.campId = campId;
    }
}

// State: this entity is a camp. Single-tile for now. The marker lets
// anything that cares about "the camp" find it — hunters returning home,
// future raiders, the player, rendering. Storage is a Container on the
// same entity.
public class Camp { }
