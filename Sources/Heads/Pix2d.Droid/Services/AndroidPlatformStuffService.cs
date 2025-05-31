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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using File = Java.IO.File;

namespace Pix2d.Droid.Services;

public class AndroidPlatformStuffService : IPlatformStuffService
{
    //not using direct services injection to prevent circular dependencies
    private readonly IServiceProvider _serviceProvider;
    private string? _appVersion;

    public AndroidPlatformStuffService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        MainActivity.Instance.FileOpened += Instance_FileOpened;
    }
    private async void Instance_FileOpened(object? sender, IFileContentSource? fileSource)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AndroidPlatformStuffService: FileOpened event received for {fileSource?.Title ?? "null"}");
            var projectService = _serviceProvider.GetRequiredService<IProjectService>();

            if (projectService == null! || fileSource == null)
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

    public PlatformType CurrentPlatform => PlatformType.Android;

    public void OpenUrlInBrowser(string url)
    {
        var uri = global::Android.Net.Uri.Parse(url);
        var intent = new Intent(Intent.ActionView, uri);
        MainActivity.Instance.StartActivity(intent);
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

    }

    public MemoryInfo GetMemoryInfo()
    {
        return new MemoryInfo(0, 0);
    }

    public string KeyToString(VirtualKeys key)
    {
        return key.ToString();
    }

    public string GetAppVersion()
    {
        _appVersion ??= CalculateAppVersionString();
        return _appVersion;
    }

    private string CalculateAppVersionString()
    {
        Assembly assembly = Assembly.GetEntryAssembly();

        if (assembly == null)
        {
            return "Unknown Version (N/A)s";
        }

        string versionString;
        string buildString;

        var informationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (informationalVersionAttribute != null)
        {
            versionString = informationalVersionAttribute.InformationalVersion;
            var assemblyName = assembly.GetName();
            buildString = assemblyName?.Version != null ? assemblyName.Version.Build.ToString() : "N/A";
        }
        else
        {
            var assemblyName = assembly.GetName();

            if (assemblyName?.Version != null)
            {
                versionString = assemblyName.Version.ToString(3);
                buildString = assemblyName.Version.Build.ToString();
            }
            else
            {
                versionString = "N/A";
                buildString = "N/A";
            }
        }

        return $"{versionString} ({buildString})s";
    }

    public void ToggleTopmostWindow()
    {
        throw new NotImplementedException();
    }

    public bool HasKeyboard => false;
    public bool CanShare => true;
    public async void Share(IStreamExporter exporter, double scale)
    {
        try
        {
            var tempFilename = "pix2d_share" + exporter.SupportedExtensions.First();
            var sdCardPath = Path.Combine(Application.Context.ExternalCacheDir.AbsolutePath, "tmp");
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
            MainActivity.Instance.StartActivity(Intent.CreateChooser(sharingIntent, "Pix2d project"));
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public void ToggleFullscreenMode()
    {
    }

    public string GetAppFolderPath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Personal);

    public Task OpenAppDataFolder()
    {
        throw new NotImplementedException();
    }
}