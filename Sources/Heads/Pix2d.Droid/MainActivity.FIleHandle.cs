using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.App;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace Pix2d.Droid;

public partial class MainActivity
{
    private void HandleIncomingUri(Android.Net.Uri? uri)
    {
        if(!_appCreated)
            return;

        if (uri != null)
        {
            DroidLog.Info($"Handling incoming URI: {uri}");
            MainThread.BeginInvokeOnMainThread(async void () =>
            {
                try
                {
                    await AttemptOpenFile(uri, isRetry: false);
                }
                catch (Exception e)
                {
                    DroidLog.Info($"Error while loading file form url{uri}, {e.Message}");
                }
            });
        }
    }

    private async Task AttemptOpenFile(Android.Net.Uri uri, bool isRetry)
    {
        DroidLog.Info($"AttemptOpenFile: URI={uri}, IsRetry={isRetry}");
        try
        {
            var fileSource = new AndroidFileContentSource(uri);
            DroidLog.Info($"AttemptOpenFile: Created fileSource for URI {uri}. Not opening stream yet.");
            FileOpened?.Invoke(this, fileSource);

            if (_uriAwaitingSafPermission != null && _uriAwaitingSafPermission.Equals(uri))
                _uriAwaitingSafPermission = null;
        }
        catch (Java.Lang.SecurityException ex)
        {
            DroidLog.Info($"AttemptOpenFile: Caught SecurityException for URI {uri}. Message: {ex.Message}");

            if (isRetry)
            {
                ShowErrorDialog($"Can open file. {ex.Message}", "Error file open");
                _uriAwaitingSafPermission = null;
            }
            else
            {
                _uriAwaitingSafPermission = uri;
                ShowSafRequiredMessage();
            }
        }
        catch (Exception ex)
        {
            DroidLog.Info($"AttemptOpenFile: Caught general Exception for URI {uri}. Message: {ex.Message}");
            ShowErrorDialog($"Произошла ошибка при подготовке к открытию файла: {ex.Message}", "Ошибка Подготовки Файла");
            _uriAwaitingSafPermission = null;
        }
    }

    private void ShowSafRequiredMessage()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var builder = new AlertDialog.Builder(this);
            builder.SetTitle("Need permission");
            builder.SetMessage("Pix2d needs permission to open this file. Please select file again");
            builder.SetPositiveButton("Select file", (dialog, which) =>
            {
                LaunchSafPicker(); 
            });
            builder.SetNegativeButton("Cancel", (dialog, which) =>
            {
                DroidLog.Info("SAF request cancelled by user.");
                _uriAwaitingSafPermission = null; 
            });
            builder.Create()?.Show();
        });
    }

    private void ShowErrorDialog(string message, string title)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var builder = new AlertDialog.Builder(this);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetPositiveButton("ОК", (dialog, which) => { /*do nothing, just close dialog */});
            builder.Create()?.Show();
        });
    }


    // SAF picker (ACTION_OPEN_DOCUMENT)
    private void LaunchSafPicker()
    {
        DroidLog.Info("Launching SAF picker...");
        Intent intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*"); 

        if (_uriAwaitingSafPermission != null && OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            try
            {
                SetInitialUri(intent, _uriAwaitingSafPermission);
                DroidLog.Info($"SAF picker hint: using EXTRA_INITIAL_URI = {_uriAwaitingSafPermission}");
            }
            catch (Exception ex)
            {
                DroidLog.Info($"Could not set EXTRA_INITIAL_URI: {ex.Message}");
            }
        }

        try
        {
            StartActivityForResult(intent, ReadRequestCode);
        }
        catch (Exception ex)
        {
            DroidLog.Info($"Failed to launch SAF picker: {ex.Message}");
            ShowErrorDialog($"Can't run system file manager: {ex.Message}", "Launch Error");
            _uriAwaitingSafPermission = null;
        }
    }

    [SupportedOSPlatform("android26.0")]
    private static void SetInitialUri(Intent intent, Android.Net.Uri initialUri)
    {
        intent.PutExtra(DocumentsContract.ExtraInitialUri, initialUri);
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        DroidLog.Info($"OnActivityResult: requestCode={requestCode}, resultCode={resultCode}");

        if (requestCode == ReadRequestCode)
        {
            if (resultCode == Result.Ok && data != null && data.Data != null)
            {
                Android.Net.Uri safUri = data.Data;
                DroidLog.Info($"SAF picker returned URI: {safUri}");

                var takeFlags = ActivityFlags.GrantReadUriPermission;
                takeFlags |= ActivityFlags.GrantWriteUriPermission;

                try
                {
                    // Получаем доступ к ContentResolver Activity
                    ContentResolver? resolver = this.ContentResolver;
                    if (resolver != null)
                    {
                        resolver.TakePersistableUriPermission(safUri, takeFlags);
                        DroidLog.Info($"Took persistable URI permission for: {safUri} with flags {takeFlags}");

                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await AttemptOpenFile(safUri, isRetry: true);
                        });

                    }
                    else
                    {
                        DroidLog.Info("ContentResolver is null in OnActivityResult.");
                        ShowErrorDialog("Can't get file management service", "System error");
                        _uriAwaitingSafPermission = null;
                    }
                }
                catch (Exception ex)
                {
                    DroidLog.Info($"Error taking persistable URI permission for {safUri}: {ex.Message}");
                    ShowErrorDialog($"Error taking persistable URI permission for : {ex.Message}", "Permission error");
                    _uriAwaitingSafPermission = null;
                }

            }
            else
            {
                DroidLog.Info("SAF picker cancelled or returned non-OK result.");
                ShowErrorDialog("File selection cancelled.", "File open");
                _uriAwaitingSafPermission = null;
            }
        }
    }

    internal ContentResolver? GetContentResolverHelper() => ContentResolver;
}
