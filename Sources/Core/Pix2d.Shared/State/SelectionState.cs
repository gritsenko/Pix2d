using Pix2d.Abstract.State;
using SkiaSharp;

namespace Pix2d.State;

public class SelectionState : StateBase
{
    public bool IsUserSelecting { get; set; }

    public SKSize UserSelectingFrameSize { get; set; }
}