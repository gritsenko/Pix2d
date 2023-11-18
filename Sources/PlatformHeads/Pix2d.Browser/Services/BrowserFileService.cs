using System.Diagnostics;
using Avalonia.Platform.Storage;
using Mvvm.Messaging;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Services;

namespace Pix2d.Browser.Services;

public class BrowserFileService : AvaloniaFileService
{
    public BrowserFileService(IDialogService dialogService, IMessenger messenger) : base(dialogService, messenger)
    {
    }

    protected override IFileContentSource? GetFileSource(IStorageFile? file)
    {
        Debug.WriteLine(file.GetType().Name);
        return file == null ? null : new AvaloniaFileSource(file);
    }
}