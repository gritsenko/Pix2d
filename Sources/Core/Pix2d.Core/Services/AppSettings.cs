using Pix2d.Primitives.Drawing;
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

    /// <summary>
    /// Brush presets the user saved (see <see cref="Pix2d.Primitives.Drawing.BrushPresetData"/>). Persisted by
    /// <c>DrawingService</c> and appended after the built-in presets on startup. Built-ins are never stored, so
    /// they stay free to evolve between releases.
    /// </summary>
    public List<BrushPresetData>? UserBrushPresets { get; set; }
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

    /// <summary>Auto-open the transform editor after a selection is made (Settings toggle, read into <c>AppState</c> at startup).</summary>
    public bool IsAutoOpenTransformEditorAfterSelectionEnabled { get; set; } = true;

    /// <summary>ISO-8601 ("O") timestamp of the last update check; throttles <c>UpdateService</c> to one check per interval.</summary>
    public string? LastUpdateCheckUtc { get; set; }

    // In-app review funnel — see ReviewService / AndroidReviewService.
    // NOTE: ISettingsService resolves keys to properties *on this class* by name (case-insensitive
    // reflection). A key with no property here makes Set() a silent no-op and Get() return default —
    // which is exactly how the whole rate-prompt gate came to be bypassed in 3.11.1: "LaunchTime" read
    // back as default(DateTime), so DateTime.Now - launchTime was ~739820 days, instantly satisfying the
    // 2-hour work-time gate and showing up as the nonsense "workTime=739820+days" in the funnel events.
    // Every key used with ISettingsService must have a property here.
    /// <summary>Wall-clock timestamp of the current launch; the span since it is folded into <see cref="TotalWorkTimeTicks"/> at the start of the next session.</summary>
    public DateTime LaunchTime { get; set; }

    /// <summary>Cumulative work time across all previous sessions, in <see cref="TimeSpan"/> ticks. Stored as a <c>long</c> because it round-trips through System.Text.Json unambiguously.</summary>
    public long TotalWorkTimeTicks { get; set; }

    /// <summary>Set once the user accepted the rate prompt — terminates the funnel permanently.</summary>
    public bool IsAppReviewed { get; set; }

    /// <summary>Earliest time the rate prompt may be shown again (escalating defer schedule, see <c>ReviewService.DefferNextReviewPrompt</c>).</summary>
    public DateTime NextPromptTime { get; set; }

    /// <summary>How many times the rate prompt has been shown; drives the escalating defer schedule.</summary>
    public int AppReviewPromptsCount { get; set; }

    /// <summary>Android only: true once Google's in-app review dialog has been requested (its quota allows it roughly once), after which the funnel opens the store page instead.</summary>
    public bool IsInAppReviewShown { get; set; }
}
