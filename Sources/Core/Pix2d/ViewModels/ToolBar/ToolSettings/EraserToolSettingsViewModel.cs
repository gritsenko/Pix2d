using Pix2d.ViewModels.ToolBar.ToolSettings;

namespace Pix2d.ViewModels.ToolSettings
{
    public class EraserToolSettingsViewModel : BrushToolSettingsViewModel
    {
        public EraserToolSettingsViewModel(IDrawingService drawingService, AppState appState) : base(drawingService, appState)
        {
            ShowColorPicker = false;
        }
    }
}