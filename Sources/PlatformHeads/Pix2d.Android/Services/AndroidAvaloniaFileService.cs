using System.IO;
using Avalonia.Platform.Storage;
using Mvvm.Messaging;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Uri = Android.Net.Uri;

namespace Pix2d.Android.Services;

public class AndroidAvaloniaFileService : Pix2d.Services.AvaloiaFileService
{

    protected override IFileContentSource? GetFileSource(IStorageFile? file)
    {
        var ext = Path.GetExtension(file.Name);
        var uri = Uri.Parse(file.Path.AbsoluteUri);
        return new AndroidFileContentSource(ext, uri);
    }

    public AndroidAvaloniaFileService(IDialogService dialogService, IMessenger messenger) : base(dialogService, messenger)
    {
    }
}