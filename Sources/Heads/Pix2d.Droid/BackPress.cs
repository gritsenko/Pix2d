using System;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Activity;
using Pix2d.Abstract.Services;

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
            // Mark the shutdown as deliberate BEFORE terminating, so the next launch doesn't read the
            // OS exit reason as a crash. Then exit via System.exit (REASON_EXIT_SELF) instead of
            // Process.KillProcess (REASON_SIGNALED, which the crash detector treats as a native crash).
            MainActivity.MarkCleanExitSafely();
            activity.FinishAndRemoveTask();
            Java.Lang.JavaSystem.Exit(0);
        }
        else
        {
            ShowExitHintToast();
            backPressed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    // A plain Toast.MakeText is force-decorated by Android 12+ with a system app-icon chip that we
    // can't style — on some skins it renders as a generic "technical" glyph rather than the Pix2d
    // icon. A custom toast view is the only way to control that, and it's allowed here because the
    // double-back exit is always in the foreground. We render the real Pix2d launcher icon + hint.
    private void ShowExitHintToast()
    {
        var message = GetExitHintText();
        try
        {
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            int Dp(int v) => (int)(v * density + 0.5f);

            var layout = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
            layout.SetGravity(GravityFlags.CenterVertical);
            layout.SetPadding(Dp(14), Dp(10), Dp(16), Dp(10));

            var background = new GradientDrawable();
            background.SetColor(Color.Argb(235, 38, 38, 38));
            background.SetCornerRadius(Dp(22));
            layout.Background = background;

            var icon = new ImageView(activity);
            icon.SetImageResource(Resource.Mipmap.ic_launcher);
            icon.LayoutParameters = new LinearLayout.LayoutParams(Dp(26), Dp(26)) { RightMargin = Dp(10) };
            layout.AddView(icon);

            var text = new TextView(activity) { Text = message };
            text.SetTextColor(Color.White);
            text.SetTextSize(ComplexUnitType.Sp, 14);
            layout.AddView(text);

#pragma warning disable CA1422 // Custom toast views are obsolete since API 30 but still supported for foreground apps.
            var toast = new Toast(activity) { Duration = ToastLength.Long, View = layout };
            toast.Show();
#pragma warning restore CA1422
        }
        catch
        {
            // A cosmetic hint must never break the back-press flow; fall back to the plain toast.
            try { Toast.MakeText(activity, message, ToastLength.Long)?.Show(); }
            catch { }
        }
    }

    private static string GetExitHintText()
    {
        const string fallback = "Press back again to exit";
        try
        {
            if (EditorApp.Pix2dBootstrapper?.GetServiceProvider() is { } sp
                && sp.GetService(typeof(ILocalizationService)) is ILocalizationService loc)
            {
                var localized = loc[fallback];
                if (!string.IsNullOrWhiteSpace(localized))
                    return localized;
            }
        }
        catch
        {
        }
        return fallback;
    }
}
