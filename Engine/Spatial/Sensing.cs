namespace Engine;

// State: how far an entity can see. Used by flee, hunt, feeding, and mate-seeking.
public class Sensing
{
    public int VisionRange { get; }

    public Sensing(int visionRange)
    {
        VisionRange = visionRange;
    }
}
