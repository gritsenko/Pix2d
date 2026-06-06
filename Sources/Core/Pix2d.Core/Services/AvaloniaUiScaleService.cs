namespace Pix2d.Services;

public class AvaloniaUiScaleService : IUiScaleService
{
    private readonly ISettingsService _settingsService;

    public AvaloniaUiScaleService(ISettingsService settingsService, AppState appState)
    {
        _settingsService = settingsService;

        var currentScale = _settingsService.Get<double?>("UiScale") ?? 1;
        appState.UiScale = currentScale;
    }

    public void SetUiScale(double scale)
    {
        // Persist regardless of platform. Desktop hosts the view in a MainWindow,
        // but Android/WASM attach the HostView directly without one — relying on
        // MainWindow here meant the scale was never applied nor saved on those heads.
        _settingsService.Set("UiScale", scale);

        (Avalonia.Application.Current as EditorApp)?.HostView?.SetUiScale(scale);
    }
}