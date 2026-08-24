namespace Pix2d.Droid;

/// <summary>
/// Logging for the Android file-open path.
///
/// <para><see cref="System.Diagnostics.Debug.WriteLine(string)"/> goes nowhere on a device — no trace
/// listener is attached, and the calls are compiled out of Release entirely — so the whole
/// "open with Pix2d" flow (which only ever reported itself that way) was invisible in logcat and had
/// to be diagnosed by reading code. <see cref="Android.Util.Log"/> always lands, so this flow can be
/// followed live with <c>adb logcat -s Pix2d</c>.</para>
/// </summary>
internal static class DroidLog
{
    public const string Tag = "Pix2d";

    public static void Info(string message) => Android.Util.Log.Info(Tag, message);

    public static void Warn(string message) => Android.Util.Log.Warn(Tag, message);
}
