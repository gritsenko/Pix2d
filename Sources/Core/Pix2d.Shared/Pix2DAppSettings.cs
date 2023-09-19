using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Pix2d;

public class Pix2DAppSettings
{
    public const SKColorType ColorType = SKColorType.Rgba8888;
    public Pix2DAppMode AppMode { get; set; }

    public List<Type> Plugins { get; set; } = new List<Type>();
}