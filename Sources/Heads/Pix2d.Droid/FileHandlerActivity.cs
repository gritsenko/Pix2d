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

// The one broad claim, and it is load-bearing: `.pix2d`/`.piskel` have no registered MIME type, so
// Android's MimeUtils reports them as application/octet-stream, and many file managers hand out a
// MediaStore URI (content://media/external/file/2342) whose path contains no file name at all — the
// path filters below cannot match it, so without this a .pix2d file has no handler and the file
// manager just says it cannot open the object. Verified on device with
// `cmd package query-activities`. This claims "unknown binary" broadly, but NOT the file types
// Android does know: APK (application/vnd.android.package-archive), PDF, zip, audio, video, images.
// The trade-off is that Pix2d appears for other extension-less/unknown files; opening one now fails
// with a plain "not supported" message (NewSceneFactory) instead of a crash.
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataMimeType = "application/octet-stream")]

// Extension-based association for formats with no registered MIME type. Two filters: providers
// that send no type at all, and providers that send some generic type. Both are pinned to the
// path patterns, which is what keeps `*/*` from meaning "everything" this time.
// Two traps here, both verified with `cmd package query-activities`:
// pathPattern is case-SENSITIVE (hence the upper-case spellings), and its SIMPLE_GLOB
// matcher does NOT backtrack, so a single `.*\.pix2d` matches "sprite.pix2d" but NOT
// "my.project.pix2d" — one leading `.*` cannot give back the dot it swallowed. Hence one
// pattern per number of dots in the name (covers up to three; a fourth is not worth the
// manifest weight). `pathAdvancedPattern`, which does backtrack, is API 26+ while minSdk
// here is 25 — on older platforms the attribute is ignored, which would strip the path
// constraint from the `*/*` filter below and turn it back into "claim every file".
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes = ["content", "file"],
    DataHost = "*",
    DataPathPatterns = [
        ".*\\.pix2d", ".*\\..*\\.pix2d", ".*\\..*\\..*\\.pix2d",
        ".*\\.PIX2D", ".*\\..*\\.PIX2D", ".*\\..*\\..*\\.PIX2D",
        ".*\\.pxm", ".*\\..*\\.pxm", ".*\\..*\\..*\\.pxm",
        ".*\\.PXM", ".*\\..*\\.PXM", ".*\\..*\\..*\\.PXM",
        ".*\\.piskel", ".*\\..*\\.piskel", ".*\\..*\\..*\\.piskel",
        ".*\\.PISKEL", ".*\\..*\\.PISKEL", ".*\\..*\\..*\\.PISKEL"
    ])]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes = ["content", "file"],
    DataHost = "*",
    DataMimeType = "*/*",
    DataPathPatterns = [
        ".*\\.pix2d", ".*\\..*\\.pix2d", ".*\\..*\\..*\\.pix2d",
        ".*\\.PIX2D", ".*\\..*\\.PIX2D", ".*\\..*\\..*\\.PIX2D",
        ".*\\.pxm", ".*\\..*\\.pxm", ".*\\..*\\..*\\.pxm",
        ".*\\.PXM", ".*\\..*\\.PXM", ".*\\..*\\..*\\.PXM",
        ".*\\.piskel", ".*\\..*\\.piskel", ".*\\..*\\..*\\.piskel",
        ".*\\.PISKEL", ".*\\..*\\.PISKEL", ".*\\..*\\..*\\.PISKEL"
    ])]
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
                DroidLog.Info($"FileHandlerActivity received URI: {uri}");
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
            DroidLog.Info($"Error in FileHandlerActivity: {ex.Message}");
        }
        Finish();
    }
}