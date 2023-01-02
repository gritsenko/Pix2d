using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;

namespace Pix2d.ViewModels.ToolSettings
{
    public class EraserToolSettingsViewModel : BrushToolSettingsViewModel
    {
        public EraserToolSettingsViewModel(IDrawingService drawingService, IAppState appState) : base(drawingService, appState)
        {
            ShowColorPicker = false;
        }
    }
}