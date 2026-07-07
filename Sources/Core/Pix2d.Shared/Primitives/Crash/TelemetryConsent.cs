namespace Pix2d.Primitives.Crash;

/// <summary>
/// Unified tri-state consent for anonymous telemetry — covers BOTH usage/conversion analytics
/// (AppStat) and critical-crash reporting (Sentry). Strict opt-in: nothing is sent until the user
/// explicitly chooses <see cref="Allowed"/>. Surfaced by the first-launch consent dialog and, as a
/// fallback, by the crash-report dialog.
/// </summary>
public enum TelemetryConsent
{
    Unset = 0,
    Allowed = 1,
    Denied = 2,
}
