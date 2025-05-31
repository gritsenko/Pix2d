using System.IO;
using Avalonia.Platform.Storage;
using Mvvm.Messaging;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Uri = Android.Net.Uri;

namespace Pix2d.Droid.Services;

public class AndroidAvaloniaFileService(
    IMessenger messenger,
    IPlatformStuffService platformStuffService,
    ISettingsService settingsService)
    : AvaloniaFileService(messenger, platformStuffService, settingsService)
{
    protected override IFileContentSource? GetFileSource(IStorageFile? file)
    {
        var ext = Path.GetExtension(file.Name);
        var uri = Uri.Parse(file.Path.AbsoluteUri);
        return new AndroidFileContentSource(uri, ext);
    }

}