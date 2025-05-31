using Pix2d.Abstract.State;
using SkiaSharp;

namespace Pix2d.State;

public class SelectionState : StateBase
{
    public bool IsUserSelecting
    {
        get => Get<bool>();
        set => Set(value);
    }

    public SKSize UserSelectingFrameSize
    {
        get => Get<SKSize>();
        set => Set(value);
    }
}