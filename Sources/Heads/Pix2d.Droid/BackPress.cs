using System;
using Android.App;
using Android.OS;
using Android.Widget;
using AndroidX.Activity;

namespace Pix2d.Droid;

internal class BackPress : OnBackPressedCallback
{
    private readonly Activity activity;
    private long backPressed;

    public BackPress(Activity activity) : base(true)
    {
        this.activity = activity;
    }

    public override void HandleOnBackPressed()
    {
        const int delay = 2000;
        if (backPressed + delay > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            MainActivity.SaveSessionSafely();
            activity.FinishAndRemoveTask();
            Process.KillProcess(Process.MyPid());
        }
        else
        {
            Toast.MakeText(activity, "Close", ToastLength.Long)?.Show();
            backPressed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}