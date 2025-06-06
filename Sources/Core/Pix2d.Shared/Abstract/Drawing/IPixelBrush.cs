﻿using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public interface IPixelBrush
{
    SKPointI PixelOffset { get; }

    SKBitmap GetPreviewBitmap(float scale);
    SKSurface GetPreviewSurface(SKColor color, float scale);

    int Size { get; }
    float Opacity { get; }
    Task InitBrush(float scale, float opacity, float spacing);

    bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure, bool ignoreSpacing = false);
    bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing);
}