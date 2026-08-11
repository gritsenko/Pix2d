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

    /// <summary>
    /// OS-reported exit status. On Android this is <c>ApplicationExitInfo.Status</c>, which for a
    /// <c>Signaled</c> exit is the <b>signal number</b> (11 = SIGSEGV, 6 = SIGABRT, 9 = SIGKILL).
    /// Reading it structurally beats parsing the trace text, whose format drifts across Android
    /// versions and OEMs — and it is the only way to tell a real native crash from an OEM
    /// low-memory kill, which is also reported as <c>Signaled</c>.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// True when the exit is a SIGKILL delivered by the OS/OEM low-memory killer rather than a
    /// genuine crash. Android reports these as <c>Signaled</c>, so they land in
    /// <see cref="LikelyCrash"/>; they must not be forwarded as crashes or fleet telemetry fills
    /// with phantom "native crashes" from ordinary background eviction.
    /// </summary>
    public bool IsLowMemoryKill => Status == 9;
}
