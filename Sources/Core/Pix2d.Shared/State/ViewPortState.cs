using SkiaSharp;

namespace Pix2d.State;

public class ViewPortState : StateBase
{
    public bool ShowGrid
    {
        get => Get(false);
        set => Set(value);
    }

    public SKSize GridSpacing
    {
        get => Get(new SKSize(8,8));
        set => Set(value);
    }

    /// <summary>
    /// Last viewport zoom for this project. 0 means "never framed" — the
    /// activation flow falls back to ShowAll() in that case.
    /// </summary>
    public float Zoom
    {
        get => Get(0f);
        set => Set(value);
    }

    public SKPoint Pan
    {
        get => Get(SKPoint.Empty);
        set => Set(value);
    }
}