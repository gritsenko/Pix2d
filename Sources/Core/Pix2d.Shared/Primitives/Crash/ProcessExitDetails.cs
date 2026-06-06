#nullable enable
namespace Pix2d.Primitives.Crash;

/// <summary>
/// Platform-reported description of how the <b>previous</b> OS process of the app terminated.
/// On Android this is sourced from <c>ActivityManager.getHistoricalProcessExitReasons</c>
/// (API 30+), which is the only way to learn about native crashes / ANRs / OS kills that the
/// managed exception handlers never observe. Heads that cannot provide this return <c>null</c>.
/// </summary>
public sealed class ProcessExitDetails
{
    /// <summary>True when the OS attributes the exit to an abnormal cause (crash, native crash, ANR).</summary>
    public bool LikelyCrash { get; set; }

    /// <summary>Human-readable reason name (e.g. "ReasonCrashNative", "ReasonAnr", "ReasonUserRequested").</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Free-form OS description of the exit, if any.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Wall-clock timestamp of the exit in epoch milliseconds — used to de-duplicate across launches.</summary>
    public long TimestampMs { get; set; }

    /// <summary>Tombstone / ANR trace captured by the OS for native crashes and ANRs, if available.</summary>
    public string? TraceText { get; set; }
}
