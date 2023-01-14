using System.Collections.Generic;
using Pix2d.Abstract.State;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.State
{
    public class DrawingState : StateBase, IDrawingState
    {
        public SKColor CurrentColor { get; set; } = SKColor.Parse("d2691e");
        public List<BrushSettings> BrushPresets { get; set; } = new();
        public BrushSettings CurrentBrushSettings { get; set; }
        public bool IsPixelPerfectDrawingModeEnabled { get; set; }
        public bool HasSelection { get; set; }
    }
}