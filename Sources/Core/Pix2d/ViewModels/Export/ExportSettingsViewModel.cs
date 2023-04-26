using System;
using Mvvm;
using SkiaSharp;

namespace Pix2d.ViewModels.Export
{
    public class ExportSettingsViewModel : ViewModelBase
    {
        public AppState AppState { get; }
        private readonly ImageExportSettings _settings = new ImageExportSettings();
        public SKSize ExportNodesSize { get; set; }

        public event EventHandler SettingsChanged;

        public int Scale
        {
            get { return _settings.Scale; }
            set
            {
                _settings.Scale = value;
                OnSettingsChanged();
                OnPropertyChanged();
            }
        }

        public int ResultWidth
        {
            get => Get<int>();
            set => Set(value);
        }

        public int ResultHeight
        {
            get => Get<int>();
            set => Set(value);
        }

        protected virtual void OnSettingsChanged()
        {
            CalculateDimensions();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public ImageExportSettings GetSettings()
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(AppState.CurrentProject.Title);

            //on android there is an Issue: If file with the same name already exists, save file picker generates incorrect suggested filename (adds suffix to extesion) so this file will not have valid extension
            //so we add timestamp to filename
            if (Pix2DApp.CurrentPlatform == PlatformType.Android)
                fileName += "_" + DateTime.Now.ToString("s").Replace(":", "").Replace("-", "");

            _settings.DefaultFileName = fileName;
            return _settings;
        }

        public ExportSettingsViewModel(AppState appState)
        {
            AppState = appState;
        }

        public void CalculateDimensions()
        {
            var pixelSpacing = 0;
            var marginX = 0;
            var marginY = 0;

            var w = ExportNodesSize.Width;
            var h = ExportNodesSize.Height;
            ResultWidth = (int)(w * (_settings.Scale + pixelSpacing) - pixelSpacing);
            ResultHeight = (int)(h * (_settings.Scale + pixelSpacing) - pixelSpacing);

            ResultWidth += marginX * 2;
            ResultHeight += marginY * 2;

        }

        public void SetSpritesheetColumns(int spsColumns)
        {
            _settings.SpritesheetColumns = spsColumns;
        }

        public int CalculateColumns()
        {
            CalculateDimensions();
            return 3;
        }

        public void SetBounds(SKRect rect)
        {
            ExportNodesSize = rect.Size;
            CalculateDimensions();
        }

    }
}