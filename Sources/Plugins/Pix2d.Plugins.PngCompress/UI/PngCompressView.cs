using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.UI;
using Pix2d.UI.Shared;
using System.Diagnostics;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Pix2d.Common.FileSystem;

namespace Pix2d.Plugins.PngCompress.UI;

public class PngCompressView : PopupView, IToolPanel
{
    protected override object Build()
    {
        Header = "Compress png";
        return BuildPopup(() => //build popup itself
            new Border().Background(Brushes.Gray)
                .Width(300)
                .Height(400)
                .Child(
                    new Grid()
                        .Rows("*,40")
                        .Children(
                            new ListBox()
                                .ItemsSource(() => Files)
                                .ItemTemplate((IFileContentSource item) => new TextBlock().Text(item?.Title ?? "-")),

                            new Button().Row(1)
                                .Content("Open files")
                                .OnClick(OnOpenFilesClicked)
                        )
                )
        );
    }

    [Inject] private IFileService FileService { get; set; } = null!;

    public IFileContentSource[] Files { get; set; }

    public int QualityMin { get; set; } = 256;

    public PngCompressView()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private async void OnOpenFilesClicked(RoutedEventArgs obj)
    {
        var files = await FileService.OpenFileWithDialogAsync(["png"], true, "pngCompress");
        Files = files.ToArray();
        StateHasChanged();
        CompressAssets();
    }


    public void CompressAssets()
    {
        var projectPath = Path.GetDirectoryName(Files[0].Path);

        var compressedDir = new DirectoryInfo(Path.Combine(projectPath, $"Compressed_{QualityMin}_colors"));

        if (compressedDir.Exists)
        {
            compressedDir.Delete(true);
        }

        compressedDir.Create();


        foreach (var file in Files)
        {
            CompressImage(compressedDir, file);
        }
    }

    private void CompressImage(DirectoryInfo compressedDir, IFileContentSource fileSrc)
    {
        Debug.WriteLine("Compressing " + fileSrc.Title);

        try
        {
            var dest = Path.Combine(compressedDir.FullName, fileSrc.Title);
            var src = fileSrc.Path;
            CompressWithImageSharp(src, dest);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "Can't compress asset: " + ex.Message + " just copying");
        }
    }

    private void CompressWithImageSharp(string src, string dest)
    {
        var maxColors = QualityMin;

        using (var img = SixLabors.ImageSharp.Image.Load(src))
        using (var output = System.IO.File.OpenWrite(dest))
        {

            //img.Quantize(Quantization.Wu);
            var encoder = new PngEncoder()
            {
                Quantizer = new WuQuantizer(new QuantizerOptions() { MaxColors = maxColors, Dither = null }),
                CompressionLevel = PngCompressionLevel.BestCompression,
                TransparentColorMode = PngTransparentColorMode.Clear,
                ColorType = PngColorType.Palette,
                BitDepth = PngBitDepth.Bit8,
            };
            img.Save(output, encoder);
        }

        File.Move(dest, src, true);
    }


    private void OnDragLeave(object? sender, DragEventArgs e)
    {
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var hasFiles = e.Data.GetDataFormats().Any(x => x == "Files");
        if (hasFiles)
            e.DragEffects = DragDropEffects.Copy;

        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var data = e.Data.Get("Files");

        if (data == null)
            return;

        var droppedFiles = data as IEnumerable<IStorageItem>;

        var fileSources = new List<IFileContentSource>();
        foreach (var storageFile in droppedFiles.OfType<IStorageFile>())
        {
            var path = System.Net.WebUtility.UrlDecode(storageFile.Path.AbsolutePath);

            var fileSource = new NetFileSource(path);

            if (path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
            {
                //await ps.OpenFilesAsync(new[] { fileSource });
                Debug.WriteLine("Compressing file " + fileSource.Title);
                fileSources.Add(new NetFileSource(path));
            }
        }

        Files = fileSources.ToArray();
        StateHasChanged();

        CompressAssets();

        e.Handled = true;
    }

    // Returns the human-readable file size for an arbitrary, 64-bit file size 
    // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
    public string GetBytesReadable(long i)
    {
        // Get absolute value
        long absolute_i = (i < 0 ? -i : i);
        // Determine the suffix and readable value
        string suffix;
        double readable;
        if (absolute_i >= 0x1000000000000000) // Exabyte
        {
            suffix = "EB";
            readable = (i >> 50);
        }
        else if (absolute_i >= 0x4000000000000) // Petabyte
        {
            suffix = "PB";
            readable = (i >> 40);
        }
        else if (absolute_i >= 0x10000000000) // Terabyte
        {
            suffix = "TB";
            readable = (i >> 30);
        }
        else if (absolute_i >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = (i >> 20);
        }
        else if (absolute_i >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = (i >> 10);
        }
        else if (absolute_i >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = i;
        }
        else
        {
            return i.ToString("0 B"); // Byte
        }
        // Divide by 1024 to get fractional value
        readable = (readable / 1024);
        // Return formatted number with suffix
        return readable.ToString("0.### ") + suffix;
    }

}