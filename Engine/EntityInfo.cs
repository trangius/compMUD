namespace Engine;

// State: display name — shown in logs, debug info, and eventually to the player.
public class Named
{
    public string name = "";
}

// State: how this entity looks. Frontends map spriteId to their own visuals.
// Layer controls draw order and what counts as "open ground."
public class Appearance
{
    public string spriteId = "";
    public int layer;
}
