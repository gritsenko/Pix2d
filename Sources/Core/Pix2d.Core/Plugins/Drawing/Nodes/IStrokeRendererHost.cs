using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

internal interface IStrokeRendererHost : IDrawingLayer
{
    SKSize Size { get; }
    SKBitmap WorkingBitmap { get; }
    SKMatrix GetGlobalTransform();
    bool IsInBounds(SKPointI pos);
}