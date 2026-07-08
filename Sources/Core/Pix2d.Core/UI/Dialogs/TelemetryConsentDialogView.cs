#nullable enable
using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;
using Pix2d.Primitives.Crash;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

/// <summary>
/// First-launch, crash-independent telemetry consent prompt. Strict opt-in: the user's choice is
/// stored via <see cref="ICrashReportService.SetTelemetryConsent"/>, which flips analytics + crash
/// telemetry on immediately when Allowed (see the bootstrapper's TelemetryConsentChanged wiring).
/// Dismissing the dialog with the window's close (X) button leaves consent Unset, so it is asked
/// again on the next launch — only the explicit buttons record a decision.
/// </summary>
public class TelemetryConsentDialogView : ViewBase, IDialogView<bool>
{
    private readonly ICrashReportService _crashService;

    public TelemetryConsentDialogView(ICrashReportService crashService)
    {
        _crashService = crashService;
    }

    public string Title { get; set; } = L("Help improve Pix2d");
    public Action<bool?> OnDialogClosed { get; set; } = null!;
    public bool DialogResult { get; private set; }

    protected override object Build() =>
        new Grid()
            // Cap the width instead of pinning it, so the dialog shrinks to fit a narrow phone-portrait
            // window (the host PopupView clamps it to the viewport as well) yet stays a comfortable
            // 400px on desktop.
            .MaxWidth(400)
            .Margin(new Thickness(16))
            .Rows("Auto,Auto,Auto")
            .Children(
                new TextBlock().Row(0)
                    .FontSize(16)
                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                    .Margin(new Thickness(0, 0, 0, 12))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(L("Share anonymous usage & crash data?")),

                new TextBlock().Row(1)
                    .FontSize(13)
                    .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
                    .Margin(new Thickness(0, 0, 0, 16))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(L("Pix2d can send anonymous usage statistics and crash reports so we can fix bugs and decide what to build next. No personal data or artwork is ever collected. Pix2d works exactly the same either way.")),

                // Two stretched columns rather than fixed button widths: the buttons always share the
                // available width and never overflow a narrow screen.
                new Grid().Row(2)
                    .Cols("*,*")
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Margin(new Thickness(0, 0, 4, 0))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Content(L("Allow"))
                            .Background(StaticResources.Brushes.AccentBrush)
                            // Accent fill needs crisp, fully-opaque white text — the theme default reads as dull grey.
                            .Foreground(Avalonia.Media.Brushes.White)
                            .OnClick(_ => Decide(true)),
                        new Button().Col(1)
                            .Classes("btn")
                            .Margin(new Thickness(4, 0, 0, 0))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Content(L("No thanks"))
                            .OnClick(_ => Decide(false))));

    private void Decide(bool allowed)
    {
        _crashService.SetTelemetryConsent(allowed ? TelemetryConsent.Allowed : TelemetryConsent.Denied);
        DialogResult = allowed;
        OnDialogClosed?.Invoke(allowed);
    }
}
