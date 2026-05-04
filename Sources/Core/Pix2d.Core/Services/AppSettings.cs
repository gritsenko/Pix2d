using Pix2d.Primitives;

namespace Pix2d.Services;

public class AppSettings
{
    // Strongly-typed: System.Text.Json restores this as SessionInfo, so SettingsService.TryGet<SessionInfo>
    // hits the "propValue is T" fast path. The previous `object?` declaration deserialized as JsonElement
    // and tripped Convert.ChangeType with "Object must implement IConvertible".
    public SessionInfo? Session { get; set; }
    public List<MruRecord>? Mru { get; set; }
    public string? Locale { get; set; }
    public bool ShowLayers { get; set; } = true;
    public string? CustomPalette { get; set; }
    public double UiScale { get; set; } = 1.0;
    public int MouseWheelBehavior { get; set; } = 1;
    public bool IsTwoFingerDoubleTapUndoEnabled { get; set; } = true;
    public int TwoFingerDoubleTapTimeoutMs { get; set; } = 200;
    public bool IsStylusModeEnabled { get; set; }
    public bool IsSingleFingerPanEnabled { get; set; }

    // Crash reporting
    /// <summary>Tri-state consent for anonymous crash telemetry: 0=unset, 1=allowed, 2=denied.</summary>
    public int CrashTelemetryConsent { get; set; } = 0;
    /// <summary>True when the previous launch was started but did not reach the "completed" marker.</summary>
    public bool LaunchInProgress { get; set; }
    /// <summary>Set when a fatal report has been written and the user has not yet dismissed the auto dialog.</summary>
    public bool HasPendingCrashReport { get; set; }
    /// <summary>Filename of the most recent crash report under the crash reports folder.</summary>
    public string? LastCrashReportId { get; set; }
}
