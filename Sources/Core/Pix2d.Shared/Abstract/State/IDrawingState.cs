using System.Collections.Generic;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Abstract.State
{
    public interface IDrawingState : IStateBase
    {
        SKColor CurrentColor { get; }

        List<BrushSettings> BrushPresets { get; }
        BrushSettings CurrentBrushSettings { get; }
        bool IsPixelPerfectDrawingModeEnabled { get; }
        bool HasSelection { get; }

    }
}