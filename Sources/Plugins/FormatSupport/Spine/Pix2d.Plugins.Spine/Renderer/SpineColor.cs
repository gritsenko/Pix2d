using Spine;

namespace Pix2d.Plugins.SpinePlugin.Renderer;

public class SpineColor
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }

    public SpineColor(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public void Set(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public SpineColor()
    {
    }

    public static SpineColor Get(Skeleton s) => new(s.R, s.G, s.B, s.A);

    public static SpineColor Get(Slot s) => new(s.R, s.G, s.B, s.A);

    public static SpineColor Get(RegionAttachment s) => new(s.R, s.G, s.B, s.A);

    public static SpineColor Get(MeshAttachment s) => new(s.R, s.G, s.B, s.A);

    public static SpineColor Get(Attachment attachment)
    {
        return attachment switch
        {
            RegionAttachment regionAttachment => Get(regionAttachment),
            MeshAttachment meshAttachment => Get(meshAttachment),
            _ => null
        };
    }
}