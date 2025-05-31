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

[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataPathPattern = ".*\\.png")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataPathPattern = ".*\\.jpg")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataPathPattern = ".*\\.gif")]

[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "*/*", DataPathPattern = ".*\\.pix2d")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "content", DataMimeType = "*/*", DataPathPattern = ".*\\.pix2d")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "file", DataPathPattern = ".*\\.pix2d")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataMimeType = "application/x-pix2d")]
public class FileHandlerActivity : Activity
{
    protected override void OnCreate(Bundle savedInstanceState)
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