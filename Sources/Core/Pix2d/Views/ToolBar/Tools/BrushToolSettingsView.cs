using Pix2d.Primitives.Drawing;
using Avalonia.Controls.Shapes;
using System.Collections.ObjectModel;
using System;
using System.Linq;

namespace Pix2d.Views.ToolBar.Tools;

public class BrushToolSettingsView : ToolSettingsView
{
    public static void ListItemStyle(ListBoxItem i) => i
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .FontSize(28)
        .MinWidth(50)
        .MinHeight(50)
        .HorizontalContentAlignment(HorizontalAlignment.Center)
        .VerticalContentAlignment(VerticalAlignment.Center);

    protected override object Build()
        => new Border()
            .Child(

                new ListBox()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                    .SelectedIndex(0)
                    .OnSelectionChanged(args =>
                    {
                        //if (args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                        //    vm.SelectShapeCommand.Execute(item.DataContext);
                    })
                    .Items(
                        new ListBoxItem()
                            .With(ListItemStyle)
                            .DataContext(ShapeType.Free)
                            .Content("\xE70F")
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .FontSize(26),

                        new ListBoxItem()
                            .With(ListItemStyle)
                            .DataContext(ShapeType.Rectangle)
                            .Content("\xe920")
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(28),

                        new ListBoxItem()
                            .With(ListItemStyle)
                            .DataContext(ShapeType.Line)
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(28)
                            .Content(
                                new Path()
                                    .Data(Geometry.Parse(
                                        "M 26.28125 4.28125 L 4.28125 26.28125 L 5.71875 27.71875 L 27.71875 5.71875 Z "))
                                    .Fill(Brushes.White)
                                    .Width(28)
                                    .Height(28)
                                    .Stretch(Stretch.Uniform)

                            ),
                        new ListBoxItem()
                            .With(ListItemStyle)
                            .DataContext(ShapeType.Oval)
                            .Content("\xe908")
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(28),

                        new ListBoxItem()
                            .With(ListItemStyle)
                            .DataContext(ShapeType.Triangle)
                            .Content("\xe927")
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(28)
                    ) //List box Items
            ); //Border child

    public IDrawingService DrawingService { get; }
    public AppState AppState { get; }
    public DrawingState DrawingState => AppState.DrawingState;

    private Primitives.Drawing.BrushSettings _currentPixelBrushSetting;
    private Primitives.Drawing.BrushSettings _currentPixelBrushPreset;

    public ObservableCollection<Primitives.Drawing.BrushSettings> BrushPresets { get; set; } = new();

    public Primitives.Drawing.BrushSettings CurrentPixelBrushPreset
    {
        get => _currentPixelBrushPreset;
        set
        {
            _currentPixelBrushPreset = value;
            var setting = _currentPixelBrushPreset.Clone();
            CurrentPixelBrushSetting = setting;
            DrawingState.CurrentBrushSettings = setting;
            OnPropertyChanged();
        }
    }

    public Primitives.Drawing.BrushSettings CurrentPixelBrushSetting
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
    
    public double BrushSetVisualPositionX { get; set; }
    public double BrushSetVisualPositionY { get; set; }

    public bool IsPixelPerfectModeEnabled
    {
        get => AppState.DrawingState.IsPixelPerfectDrawingModeEnabled;
        set => AppState.DrawingState.IsPixelPerfectDrawingModeEnabled = value;
    }

    public int SelectedIndex { get; set; } //save selected item state for settings view

    protected override void OnAfterInitialized()
    {
        AppState.DrawingState.WatchFor(x => x.CurrentBrushSettings, OnBrushChanged);

        if (!BrushPresets.Any())
        {
            foreach (var preset in AppState.DrawingState.BrushPresets)
            {
                BrushPresets.Add(preset);
            }
        }

        _currentPixelBrushSetting = DrawingState.CurrentBrushSettings.Clone();

    }

    private void OnBrushChanged()
    {
        UpdateCurrentSetting();
        OnPropertyChanged(nameof(CurrentPixelBrushSetting));
        OnPropertyChanged(nameof(BrushScale));
        OnPropertyChanged(nameof(BrushOpacity));
        OnPropertyChanged(nameof(BrushSpacing));
    }

    private void UpdateCurrentSetting()
    {
        CurrentPixelBrushSetting = DrawingState.CurrentBrushSettings;
    }
}