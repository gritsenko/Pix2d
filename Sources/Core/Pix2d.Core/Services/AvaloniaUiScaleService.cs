using Pix2d.UI;

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
        if (EditorApp.TopLevel is MainWindow wnd
            && wnd.Content is HostView hostView)
        {
            hostView.SetUiScale(scale);

            _settingsService.Set("UiScale", scale);
        }
    }
}