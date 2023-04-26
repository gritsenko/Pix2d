using Pix2d.Abstract.State;
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

}