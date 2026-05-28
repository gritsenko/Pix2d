using SkiaNodes;

namespace Pix2d.Selection;

public static class SelectionOutlineMetrics
{
    private const float BaseStrokePixels = 1.75f;
    private const float BaseDashPixels = 5f;
    private const float MaxUiScale = 2f;

    public static float GetStrokeWidthWorld(ViewPort vp)
    {
        return vp.PixelsToWorld(BaseStrokePixels * GetUiScale(vp));
    }

    public static float GetDashLengthWorld(ViewPort vp)
    {
        return vp.PixelsToWorld(BaseDashPixels * GetUiScale(vp));
    }

    private static float GetUiScale(ViewPort vp)
    {
        // PixelsToWorld keeps adorners constant in physical pixels. For selection outlines we want
        // the opposite: track system UI scaling so the marquee remains legible on dense displays.
        return Math.Clamp(vp.ScaleFactor, 1f, MaxUiScale);
    }
}