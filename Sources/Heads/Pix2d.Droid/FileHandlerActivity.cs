using Android.App;
using Android.Content;
using Android.OS;
using System;

namespace Pix2d.Droid;

[Activity(
    Theme = "@android:style/Theme.NoDisplay",
    Icon = "@mipmap/ic_launcher",
    NoHistory = true,
    Exported = true)]

// Intent filters: keep them narrow - every type listed here makes Pix2d offer itself as a
// handler for that file, and the app crashes on anything it cannot decode.
//
// The trap: `android:pathPattern` is only honoured when the filter ALSO declares a scheme AND a
// host (IntentFilter.matchData only consults mDataPaths inside the "scheme matched" branch, and
// only when mDataAuthorities matched too). A filter of `mimeType="*/*"` + `pathPattern=".*\.png"`
// therefore degenerates into "VIEW anything", which is how Pix2d ended up in the chooser for
// APKs (content:// + application/vnd.android.package-archive) and every other file type.
//
// So: standard formats are matched by their real MIME type, and Pix2d's own extensions are
// matched by path - with scheme + host present so the pattern actually applies.
// Extensions must match the importers registered in Pix2dBootstrapperDI.LoadPlugins /
// ImportAnalyzer: .pix2d/.pxm projects, .png, .jpg/.jpeg, .gif, .piskel.

// Raster images the importers can actually decode, matched by MIME type. A filter with no
// scheme accepts only empty/content/file schemes, so this never hijacks http(s) links.
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataMimeTypes = ["image/png", "image/jpeg", "image/gif"])]

// Pix2d's own MIME type, when a provider bothers to report it.
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataMimeType = "application/x-pix2d")]

// Extension-based association for formats with no registered MIME type. Two filters: providers
// that send no type at all, and providers that send some generic type. Both are pinned to the
// path patterns, which is what keeps `*/*` from meaning "everything" this time.
// Note pathPattern is case-sensitive, hence the upper-case variants.
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes = ["content", "file"],
    DataHost = "*",
    DataPathPatterns = [".*\\.pix2d", ".*\\.PIX2D", ".*\\.pxm", ".*\\.PXM", ".*\\.piskel", ".*\\.PISKEL"])]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes = ["content", "file"],
    DataHost = "*",
    DataMimeType = "*/*",
    DataPathPatterns = [".*\\.pix2d", ".*\\.PIX2D", ".*\\.pxm", ".*\\.PXM", ".*\\.piskel", ".*\\.PISKEL"])]
public class FileHandlerActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            if (Intent is { Action: Intent.ActionView, Data: not null })
            {
                var uri = Intent.Data;
                System.Diagnostics.Debug.WriteLine($"FileHandlerActivity received URI: {uri}");
                MainActivity.PendingFileUri = uri;
                var mainIntent = new Intent(this, typeof(MainActivity));
                mainIntent.SetData(uri);
                mainIntent.SetAction(Intent.ActionView);

                mainIntent.AddFlags(ActivityFlags.GrantReadUriPermission);
                //mainIntent.AddFlags(ActivityFlags.GrantWriteUriPermission);

                mainIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NewTask);
                StartActivity(mainIntent);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in FileHandlerActivity: {ex.Message}");
        }
        Finish();
    }
}