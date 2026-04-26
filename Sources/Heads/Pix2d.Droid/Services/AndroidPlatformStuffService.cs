using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using File = Java.IO.File;

namespace Pix2d.Droid.Services;

public class AndroidPlatformStuffService : IPlatformStuffService, ICrashReportShareTarget
{
    //not using direct services injection to prevent circular dependencies
    private readonly IServiceProvider _serviceProvider;
    private MainActivity? _attachedActivity;

    public PlatformType CurrentPlatform => PlatformType.Android;
    public bool IsTextInputFocused => EditorApp.TopLevel?.FocusManager?.GetFocusedElement() is TextBox;

    public AndroidPlatformStuffService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        if (MainActivity.TryGetInstance(out var activity))
            AttachActivity(activity);
    }

    internal void AttachActivity(MainActivity activity)
    {
        if (ReferenceEquals(_attachedActivity, activity))
            return;

        if (_attachedActivity != null)
            _attachedActivity.FileOpened -= Instance_FileOpened;

        _attachedActivity = activity;
        _attachedActivity.FileOpened += Instance_FileOpened;
    }

    private MainActivity? GetActivity() => _attachedActivity ?? (MainActivity.TryGetInstance(out var activity) ? activity : null);

    private async void Instance_FileOpened(object? sender, IFileContentSource? fileSource)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AndroidPlatformStuffService: FileOpened event received for {fileSource?.Title ?? "null"}");
            var projectService = _serviceProvider.GetRequiredService<IProjectService>();

            if (projectService == null || fileSource == null)
            {
                System.Diagnostics.Debug.WriteLine("AndroidPlatformStuffService: projectService is null or fileSource is null.");
                return;
            }
            await projectService.OpenFilesAsync([fileSource]);

            System.Diagnostics.Debug.WriteLine($"AndroidPlatformStuffService: Successfully called projectService.OpenFilesAsync for {fileSource.Title}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidPlatformStuffService: Caught IOException during file processing: {ex.Message}");
            var ds = _serviceProvider.GetRequiredService<IDialogService>();
            ds.Alert($"IO Error while file loading \"{fileSource?.Title ?? "unknown file"}\": {ex.Message}", "File content error");
        }
        catch (Exception ex)
        {
            // Обработка других исключений (например, ошибки парсинга файла в ProjectService)
            System.Diagnostics.Debug.WriteLine($"AndroidPlatformStuffService: Caught general Exception during file processing: {ex.Message}");

            var ds = _serviceProvider.GetRequiredService<IDialogService>(); // Убедитесь, что DialogService доступен
            ds.Alert($"Error in file opening \"{fileSource?.Title ?? "неизвестный файл"}\": \n{ex.Message}", "Error in file opening");
        }
    }

    public void OpenUrlInBrowser(string url)
    {
        var uri = global::Android.Net.Uri.Parse(url);
        var intent = new Intent(Intent.ActionView, uri);
        GetActivity()?.StartActivity(intent);
        EnsureAppFolderExists();
    }

    private void EnsureAppFolderExists()
    {
        var path = GetAppFolderPath();

        if (string.IsNullOrWhiteSpace(path))
            throw new Exception("Data folder is not initialized");

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    public void SetWindowTitle(string title)
    {
        try
        {
            if (GetActivity() is { } activity)
            {
                activity.Title = title + " - Pix2d v" + GetAppVersion();
            }
        }
        catch
        {
        }
    }

    public MemoryInfo GetMemoryInfo()
    {
        return new MemoryInfo(0, 0);
    }

    public string KeyToString(VirtualKeys key)
    {
        return key.ToString();
    }

    public string GetAppVersion() => $"{Pix2d.Common.BuildInfo.Version} droid";

    public void ToggleTopmostWindow()
    {
        throw new NotImplementedException();
    }

    public bool HasKeyboard => true;
    public bool CanShare => true;
    public async void Share(IStreamExporter exporter, double scale)
    {
        try
        {
            var tempFilename = "pix2d_share" + exporter.SupportedExtensions.First();
            var externalCacheDir = Application.Context.ExternalCacheDir?.AbsolutePath;
            if (externalCacheDir == null)
                throw new InvalidOperationException("External cache directory is not available");

            var sdCardPath = Path.Combine(externalCacheDir, "tmp");
            if (!Directory.Exists(sdCardPath))
            {
                Directory.CreateDirectory(sdCardPath);
            }

            var filePath = Path.Combine(sdCardPath, tempFilename);
            await using (var os = new FileStream(filePath, FileMode.Create))
            {
                var nodes = _serviceProvider.GetRequiredService<IExportService>().GetNodesToExport(scale);
                var source = await exporter.ExportToStreamAsync(nodes, scale);
                await source.CopyToAsync(os);
                os.Close();
            }

            var imageUri = FileProvider.GetUriForFile(Application.Context, Application.Context.PackageName + ".fileprovider",
                new File(filePath));
            var sharingIntent = new Intent();
            sharingIntent.SetAction(Intent.ActionSend);
            sharingIntent.SetType(exporter.MimeType);
            sharingIntent.PutExtra(Intent.ExtraStream, imageUri);
            sharingIntent.AddFlags(ActivityFlags.GrantReadUriPermission);
            GetActivity()?.StartActivity(Intent.CreateChooser(sharingIntent, "Pix2d project"));
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public void ToggleFullscreenMode()
    {
    }

    public void ShareCrashReportFile(string filePath, string subject)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
                return;

            var uri = FileProvider.GetUriForFile(Application.Context,
                Application.Context.PackageName + ".fileprovider",
                new File(filePath));

            var intent = new Intent();
            intent.SetAction(Intent.ActionSend);
            intent.SetType("text/plain");
            intent.PutExtra(Intent.ExtraSubject, subject);
            intent.PutExtra(Intent.ExtraStream, uri);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            GetActivity()?.StartActivity(Intent.CreateChooser(intent, subject));
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public string GetAppFolderPath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Personal);

    public Task OpenAppDataFolder()
    {
        throw new NotImplementedException();
    }
}
