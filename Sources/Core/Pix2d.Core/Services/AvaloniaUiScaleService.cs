namespace Pix2d.Services;

public class AvaloniaUiScaleService : IUiScaleService
{
    private readonly ISettingsService _settingsService;

    public AvaloniaUiScaleService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        // The saved scale is loaded into AppState at startup by Pix2dBootstrapperDI.Initialize(),
        // so this service no longer needs to do it lazily (that ran too late — only once the
        // Settings view was opened — leaving the interface at 1x after a restart).
    }

    public void SetUiScale(double scale)
    {
        // Persist regardless of platform. Desktop hosts the view in a MainWindow,
        // but Android/WASM attach the HostView directly without one — relying on
        // MainWindow here meant the scale was never applied nor saved on those heads.
        _settingsService.Set(nameof(AppState.UiScale), scale);

        (Avalonia.Application.Current as EditorApp)?.HostView?.SetUiScale(scale);
    }
}