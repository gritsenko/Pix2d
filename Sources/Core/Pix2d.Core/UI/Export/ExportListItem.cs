using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Export;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.Export;

/// <summary>
/// One row of the Export dialog's artboard list: the thumbnail, the name the output will be written under,
/// and the measured output (resolution · file count · size). Metrics arrive progressively — measuring means
/// running the real exporter per artboard — so a row starts with an empty details line and fills in.
/// </summary>
public sealed partial class ExportListItem(ExportItem item) : ObservableObject
{
    public ExportItem Item { get; } = item;

    /// <summary>Base file name the export will use — the whole point of showing the list.</summary>
    public string Name { get; } = item.Name;

    public SKBitmapObservable Thumbnail { get; } = new();

    /// <summary>False until the thumbnail has been rendered once; thumbnails don't depend on export scale,
    /// so they survive a scale change instead of being re-rendered.</summary>
    public bool HasThumbnail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackground))]
    public partial bool IsSelected { get; set; }

    /// <summary>Resolution · files · size, or a placeholder while the measurement is still running.</summary>
    [ObservableProperty]
    public partial string DetailsText { get; set; } = "…";

    public int Width { get; private set; }
    public int Height { get; private set; }
    public long Bytes { get; private set; }
    public int Files { get; private set; }

    public IBrush RowBackground => IsSelected
        ? StaticResources.Brushes.ButtonHoverBrush
        : Brushes.Transparent;

    public void SetThumbnail(SKBitmap bitmap)
    {
        HasThumbnail = true;
        Thumbnail.SetBitmap(bitmap);
    }

    public void SetMetrics(int width, int height, long bytes, int files, string filesLabel)
    {
        Width = width;
        Height = height;
        Bytes = bytes;
        Files = files;

        var size = $"{width} × {height} px";
        if (files > 1)
            DetailsText = $"{size} · {files} {filesLabel} · ~{FormatSize(bytes)}";
        else if (bytes > 0)
            DetailsText = $"{size} · ~{FormatSize(bytes)}";
        else
            DetailsText = size;
    }

    /// <summary>Measurement threw (an over-large render, a sprite the exporter can't handle). The row stays
    /// in the list — it will still be exported — but says it couldn't be sized rather than showing a lie.</summary>
    public void SetUnmeasurable(string message)
    {
        Width = 0;
        Height = 0;
        Bytes = 0;
        Files = 0;
        DetailsText = message;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024):0.##} MB";
    }
}
