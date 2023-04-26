using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using Pix2d.ViewModels.ToolSettings;

namespace Pix2d.ViewModels.ToolBar.ToolSettings;

public class BrushToolSettingsViewModel : ToolSettingsBaseViewModel
{
    public IDrawingService DrawingService { get; }
    public AppState AppState { get; }
    public DrawingState DrawingState => AppState.DrawingState;

    private BrushPresetViewModel _currentPixelBrushSetting;
    private BrushPresetViewModel _currentPixelBrushPreset;

    public ObservableCollection<BrushPresetViewModel> BrushPresets { get; set; } = new();

    public BrushPresetViewModel CurrentPixelBrushPreset
    {
        get => _currentPixelBrushPreset;
        set
        {
            _currentPixelBrushPreset = value;
            var setting = _currentPixelBrushPreset.Preset.Clone();
            CurrentPixelBrushSetting = new BrushPresetViewModel(setting);
            DrawingState.CurrentBrushSettings = setting;
            OnPropertyChanged();
        }
    }

    public BrushPresetViewModel CurrentPixelBrushSetting
    {
        get => _currentPixelBrushSetting;
        set
        {
            _currentPixelBrushSetting = value;

            OnPropertyChanged();
        }
    }

    public float BrushScale
    {
        get => DrawingState.CurrentBrushSettings.Scale;
        set => DrawingState.CurrentBrushSettings.Scale = value;
    }

    public float BrushOpacity
    {
        get => (float)Math.Floor(DrawingState.CurrentBrushSettings.Opacity * 100);
        set => DrawingState.CurrentBrushSettings.Opacity = value / 100;
    }

    public float BrushSpacing
    {
        get => (float)Math.Floor(DrawingState.CurrentBrushSettings.Spacing);
        set => DrawingState.CurrentBrushSettings.Spacing = value;
    }

    public ShapeType ShapeType
    {
        get => ((BrushTool)Tool).ShapeType;
        set => ((BrushTool)Tool).ShapeType = value;
    }

    public string ShapeTypeKey
    {
        get => ShapeType.ToString();
        set
        {
            ShapeType = (ShapeType)System.Enum.Parse(typeof(ShapeType), value);
            SessionLogger.OpLog(value);
        }
    }


    public double BrushSetVisualPositionX { get; set; }
    public double BrushSetVisualPositionY { get; set; }

    public bool IsPixelPerfectModeEnabled
    {
        get => AppState.DrawingState.IsPixelPerfectDrawingModeEnabled;
        set => AppState.DrawingState.IsPixelPerfectDrawingModeEnabled = value;
    }

    public int SelectedIndex { get; set; } //save selected item state for settings view
    public ICommand SelectShapeCommand => GetCommand<ShapeType>(OnSelectShapeCommandExecute);

    public BrushToolSettingsViewModel(IDrawingService drawingService, AppState appState)
    {
        DrawingService = drawingService;
        AppState = appState;
        ShowColorPicker = true;
        ShowBrushSettings = true;

        AppState.DrawingState.WatchFor(x => x.CurrentBrushSettings, OnBrushChanged);
    }

    private void OnBrushChanged()
    {
        UpdateCurrentSetting();
        OnPropertyChanged(nameof(CurrentPixelBrushSetting));
        OnPropertyChanged(nameof(BrushScale));
        OnPropertyChanged(nameof(BrushOpacity));
        OnPropertyChanged(nameof(BrushSpacing));
    }

    protected override void OnLoad()
    {
        if (!BrushPresets.Any())
        {
            foreach (var preset in AppState.DrawingState.BrushPresets)
            {
                BrushPresets.Add(new BrushPresetViewModel(preset));
            }
        }

        _currentPixelBrushSetting = new BrushPresetViewModel(DrawingState.CurrentBrushSettings.Clone());
    }

    private void UpdateCurrentSetting()
    {
        CurrentPixelBrushSetting = new BrushPresetViewModel(DrawingState.CurrentBrushSettings);
    }
    private void OnSelectShapeCommandExecute(ShapeType shapeType)
    {
        //backward compatibilty to UWP version
        ShapeTypeKey = shapeType.ToString();
    }


}