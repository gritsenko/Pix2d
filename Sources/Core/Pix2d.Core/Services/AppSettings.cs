using Pix2d.Primitives.Palette;

namespace Pix2d.Services;

public class AppSettings
{
    public List<MruRecord>? Mru { get; set; }
    public string? Locale { get; set; }
    public bool ShowLayers { get; set; } = true;
    public string? CustomPalette { get; set; }

    /// <summary>User's named-palette library (see <see cref="PaletteData"/>). Persisted by <c>PaletteService</c>.</summary>
    public List<PaletteData>? SavedPalettes { get; set; }
    public double UiScale { get; set; } = 1.0;
    public int MouseWheelBehavior { get; set; } = 1;
    public bool IsTwoFingerDoubleTapUndoEnabled { get; set; } = true;
    public int TwoFingerDoubleTapTimeoutMs { get; set; } = 200;
    public bool IsStylusModeEnabled { get; set; }
    public bool IsSingleFingerPanEnabled { get; set; }
    public bool IsPenHapticsEnabled { get; set; } = true;

    /// <summary>Stable, anonymous per-install id used to correlate analytics events. Random GUID, not PII.</summary>
    public string? InstallId { get; set; }

    // Telemetry
    /// <summary>Unified tri-state consent for anonymous telemetry (usage analytics + crash reports): 0=unset, 1=allowed, 2=denied. Migrated from the legacy crash-only "CrashTelemetryConsent" key.</summary>
    public int TelemetryConsent { get; set; } = 0;
    /// <summary>True when the previous launch was started but did not reach the "completed" marker.</summary>
    public bool LaunchInProgress { get; set; }
    /// <summary>Set when a fatal report has been written and the user has not yet dismissed the auto dialog.</summary>
    public bool HasPendingCrashReport { get; set; }
    /// <summary>Filename of the most recent crash report under the crash reports folder.</summary>
    public string? LastCrashReportId { get; set; }
    /// <summary>Epoch-ms timestamp of the last OS process-exit record we already turned into a report; used to avoid re-reporting the same exit on every launch.</summary>
    public long LastHandledProcessExitTimestamp { get; set; }
    /// <summary>Set right before a deliberate, user-initiated shutdown (e.g. the Android double-back exit, which self-kills the process). The OS reports that termination as SIGNALED/EXIT_SELF, so the next launch reads this one-shot marker to avoid mistaking the clean exit for a crash.</summary>
    public bool CleanExitRequested { get; set; }
}
