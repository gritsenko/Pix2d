#nullable enable
using Pix2d.Abstract.Platform.FileSystem;
using SkiaSharp;

namespace Pix2d;

public class Pix2DAppSettings
{
    public const SKColorType ColorType = SKColorType.Rgba8888;
    public Pix2DAppMode AppMode { get; set; }

    public List<Type> Plugins { get; set; } = [];
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(3);

    public bool UseInternalFolder { get; set; }
    public IFileContentSource? StartupDocument { get; set; }
}