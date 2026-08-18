using System;
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
    ISettingsService settingsService,
    IDialogService dialogService)
    : AvaloniaFileService(messenger, platformStuffService, settingsService, dialogService)
{
    protected override IFileContentSource GetFileSource(IStorageFile? file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
            
        var ext = Path.GetExtension(file.Name);
        var uri = Uri.Parse(file.Path.AbsoluteUri);
        if (uri == null)
            throw new InvalidOperationException($"Could not parse URI from file path: {file.Path.AbsoluteUri}");
            
        return new AndroidFileContentSource(uri, ext);
    }

}