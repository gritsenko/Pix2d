#nullable enable
using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;
using Pix2d.Primitives.Crash;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class CrashReportDialogView : ViewBase, IDialogView<bool>
{
    private readonly ICrashReportService _crashService;
    private readonly IPlatformStuffService _platformService;
    private readonly CrashReportSummary? _summary;
    private readonly bool _autoShown;

    public CrashReportDialogView(ICrashReportService crashService, IPlatformStuffService platformService, bool autoShown = true)
    {
        _crashService = crashService;
        _platformService = platformService;
        _autoShown = autoShown;
        _summary = crashService.PendingCrashReport ?? crashService.LoadLatestReport();
    }

    public string Title { get; set; } = "Pix2d crashed";
    public Action<bool?> OnDialogClosed { get; set; } = null!;
    public bool DialogResult { get; private set; }

    protected override object Build()
    {
        if (_summary == null)
        {
            return new Grid()
                .Rows("*,48")
                .Children(
                    new TextBlock()
                        .Margin(new Thickness(16))
                        .Text("No crash report available."),
                    new Button().Row(1)
                        .Classes("btn")
                        .Width(100)
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .Content("Close")
                        .OnClick(_ => Close(false)));
        }

        var displayText = _summary.FormatForDisplay();
        var consent = _crashService.TelemetryConsent;
        var consentChecked = consent == TelemetryConsent.Allowed;
        var showConsent = consent == TelemetryConsent.Unset && TelemetrySupportedOnThisPlatform();

        var consentToggle = new CheckBox()
            .Margin(new Thickness(0, 8, 0, 8))
            .IsChecked(consentChecked)
            .Content("Send anonymous critical crash data to help fix Pix2d");

        var summaryHeader = $"Pix2d {_summary.AppVersion} on {_summary.Platform}\n{_summary.ExceptionType}: {_summary.Message}";

        return new Grid()
            .Margin(new Thickness(16))
            .Rows("Auto,Auto,*,Auto,Auto")
            .Children(
                new TextBlock().Row(0)
                    .FontSize(18)
                    .Margin(new Thickness(0, 0, 0, 8))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(_autoShown
                        ? "Pix2d closed unexpectedly. The report below can help us diagnose what happened."
                        : "Latest crash report"),

                new TextBlock().Row(1)
                    .FontSize(13)
                    .Foreground(StaticResources.Brushes.AccentBrush)
                    .Margin(new Thickness(0, 0, 0, 8))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(summaryHeader),

                new Border().Row(2)
                    .BorderThickness(new Thickness(1))
                    .BorderBrush(Brushes.DimGray)
                    .Margin(new Thickness(0, 0, 0, 8))
                    .Child(
                        new ScrollViewer()
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Content(
                                new SelectableTextBlock()
                                    .Margin(new Thickness(8))
                                    .FontFamily(new FontFamily("Consolas, Menlo, Monospace"))
                                    .FontSize(11)
                                    .TextWrapping(TextWrapping.NoWrap)
                                    .Text(displayText))),

                showConsent
                    ? (Control)consentToggle.Row(3)
                    : new TextBlock().Row(3).IsVisible(false),

                new StackPanel().Row(4)
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Margin(new Thickness(8, 0))
                            .Width(140)
                            .Content("Send report")
                            .Background(StaticResources.Brushes.AccentBrush)
                            // Accent fill needs crisp, fully-opaque white text — the theme default reads as dull grey.
                            .Foreground(Avalonia.Media.Brushes.White)
                            .OnClick(_ =>
                            {
                                ApplyConsentFromToggle(consentToggle, showConsent);
                                TrySendReport();
                                Close(true);
                            }),
                        new Button()
                            .Classes("btn")
                            .Margin(new Thickness(8, 0))
                            .Width(120)
                            .Content("Close")
                            .OnClick(_ =>
                            {
                                ApplyConsentFromToggle(consentToggle, showConsent);
                                Close(false);
                            })));
    }

    // Platforms that ship an opt-in crash telemetry sink (Sentry): Android and the desktop family
    // (Windows / Linux / macOS, incl. the MS Store bundle). WASM/iOS produce local reports only, so
    // there's no consent to ask for there.
    private bool TelemetrySupportedOnThisPlatform() =>
        _platformService.CurrentPlatform is PlatformType.Android
            or PlatformType.WindowsDesktop
            or PlatformType.CrossPlatformDesktop
            or PlatformType.MacOS
            or PlatformType.WindowsStore;

    private void ApplyConsentFromToggle(CheckBox toggle, bool showConsent)
    {
        if (!showConsent) return;
        var allowed = toggle.IsChecked == true;
        _crashService.SetTelemetryConsent(allowed ? TelemetryConsent.Allowed : TelemetryConsent.Denied);
    }

    private void TrySendReport()
    {
        try
        {
            var path = _crashService.GetLatestReportFilePath();
            if (string.IsNullOrEmpty(path))
                return;

            // Replace .json with .txt for share — txt is human-readable and accepted by every app.
            var txtPath = System.IO.Path.ChangeExtension(path, ".txt");
            if (!System.IO.File.Exists(txtPath))
                txtPath = path;

            if (_platformService is ICrashReportShareTarget target)
                target.ShareCrashReportFile(txtPath, "Pix2d crash report");
        }
        catch
        {
        }
    }

    private void Close(bool result)
    {
        DialogResult = result;
        if (_autoShown)
            _crashService.DismissPending();
        OnDialogClosed?.Invoke(result);
    }
}
