using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;
using SkiaSharp;

namespace Pix2d.ViewModels.ToolSettings
{
    public class FillToolSettingsViewModel : ToolSettingsBaseViewModel
    {
        private SKColor _lastColor;

        public IDrawingService DrawingService { get; }
        public IAppState AppState { get; }
        public IDrawingState DrawingState => AppState.DrawingState;

        public bool EraseMode
        {
            get => Get<bool>();
            set
            {
                if (Set(value))
                {
                    SetEraseMode(value);
                }
            }
        }

        private void SetEraseMode(bool value)
        {
            if (value)
            {
                _lastColor = DrawingState.CurrentColor;
                DrawingService.SetCurrentColor(SKColor.Empty);
            }
            else
                DrawingService.SetCurrentColor(_lastColor);
        }

        public FillToolSettingsViewModel(IDrawingService drawingService, IAppState appState)
        {
            DrawingService = drawingService;
            AppState = appState;
            ShowColorPicker = true;
        }

        public override void Activated()
        {
            _lastColor = DrawingState.CurrentColor;

            if (EraseMode)
            {
                DrawingService.SetCurrentColor(SKColor.Empty);
            }
            base.Activated();
        }

        public override void Deactivated()
        {
            if(EraseMode)
                DrawingService.SetCurrentColor(_lastColor);
            base.Deactivated();
        }
    }
}