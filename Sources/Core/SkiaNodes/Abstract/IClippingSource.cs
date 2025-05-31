using SkiaSharp;

namespace SkiaNodes;

public interface IClippingSource
{
    SKNodeClipMode ClipMode { get; }

    SKRect ClipBounds { get; }
}