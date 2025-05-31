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
        //var navigation = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation;
        //if (navigation is not null && navigation.NavigationStack.Count <= 1 && navigation.ModalStack.Count <= 0)
        {
            const int delay = 2000;
            if (backPressed + delay > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
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
}