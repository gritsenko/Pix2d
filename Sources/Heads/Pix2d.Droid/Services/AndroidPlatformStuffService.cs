using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;
using SkiaNodes.Interactive;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using File = Java.IO.File;

namespace Pix2d.Droid.Services;

public class AndroidPlatformStuffService : IPlatformStuffService, ICrashReportShareTarget, IProcessExitInfoProvider
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
        // No topmost-window concept on Android.
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
        // The app-data folder is private on Android; crash reports are shared via ShareCrashReportFile instead.
        return Task.CompletedTask;
    }

    // Max amount of OS-provided tombstone / ANR trace we keep — these can be large.
    private const int ExitTraceMaxChars = 16 * 1024;

    /// <summary>
    /// Reads the most recent historical process-exit record from the OS. This is the only reliable
    /// way on Android to learn that the previous process died from a native crash, an ANR or an OS
    /// kill — none of which surface through the managed exception handlers. API 30+ only.
    /// </summary>
    public ProcessExitDetails? GetLastProcessExitDetails()
    {
        try
        {
            // ApplicationExitInfo / GetHistoricalProcessExitReasons require API 30. Using the
            // OperatingSystem guard (not a raw SdkInt check) also satisfies the CA1416 analyzer.
            if (!OperatingSystem.IsAndroidVersionAtLeast(30))
                return null;

            if (Application.Context.GetSystemService(Context.ActivityService) is not ActivityManager am)
                return null;

            // packageName=null → our own package; pid=0 → any; maxNum=1 → most recent only.
            var infos = am.GetHistoricalProcessExitReasons(null, 0, 1);
            if (infos == null || infos.Count == 0)
                return null;

            var info = infos[0];
            // info.Reason is bound as a raw int; the named values live on the ApplicationExitInfoReason enum.
            var reason = (ApplicationExitInfoReason)info.Reason;

            // Signaled = the process was taken down by a signal (SIGSEGV/SIGABRT/SIGILL …). A native
            // null-deref or a runtime-induced crash is frequently reported this way rather than as
            // CrashNative, so it must count as a crash or the report is silently dropped.
            var likelyCrash = reason is ApplicationExitInfoReason.Crash
                or ApplicationExitInfoReason.CrashNative
                or ApplicationExitInfoReason.Anr
                or ApplicationExitInfoReason.Signaled;

            string? traceText = null;
            if (reason is ApplicationExitInfoReason.Anr
                or ApplicationExitInfoReason.CrashNative
                or ApplicationExitInfoReason.Signaled)
            {
                try
                {
                    using var trace = info.TraceInputStream;
                    if (trace != null)
                    {
                        using var reader = new System.IO.StreamReader(trace);
                        var raw = reader.ReadToEnd();
                        traceText = raw.Length > ExitTraceMaxChars
                            ? raw.Substring(raw.Length - ExitTraceMaxChars)
                            : raw;
                    }
                }
                catch
                {
                    // Trace stream is best-effort; the reason alone is still useful.
                }
            }

            return new ProcessExitDetails
            {
                LikelyCrash = likelyCrash,
                Reason = reason.ToString(),
                Description = info.Description ?? string.Empty,
                TimestampMs = info.Timestamp,
                TraceText = traceText,
            };
        }
        catch
        {
            return null;
        }
    }
}
