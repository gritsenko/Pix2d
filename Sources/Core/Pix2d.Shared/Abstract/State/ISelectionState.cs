using System;
using SkiaSharp;

namespace Pix2d.Abstract.State
{
    public interface ISelectionState : IStateBase
    {
        bool IsUserSelecting { get; }
        SKSize UserSelectingFrameSize { get; }
    }
}