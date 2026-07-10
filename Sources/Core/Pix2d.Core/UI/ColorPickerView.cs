using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.State;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Pix2d.UI;

public partial class ColorPickerView(AppState appState, IMessenger messenger, IPaletteService paletteService, IDrawingService drawingService, IDialogService dialogService)
    : ViewBase<ColorPickerView.State>(new State(appState, messenger, paletteService, drawingService, dialogService))
{
    protected override object Build(State state)
    {
        return new Grid().Width(236)
            .Rows("140, Auto, *")
            .Children(
                ViewFactory.Create<Pix2dColorPicker>()
                    .Margin(10)
                    .Color(state, x => x.SelectedColor, BindingMode.TwoWay),

                new Border().Row(2).Margin(8, 8, 8, 16).MinHeight(100)
                    .Child(
                        new TabControl()
                            .SelectedIndex(state, x => x.ColorTypeIndex, BindingMode.TwoWay)
                            .Items(
                                new TabItem() //PALETTE EDITOR
                                    .Foreground(Brushes.White)
                                    .Header(L("List"))
                                    .Content(
                                        new StackPanel()
                                            .IsVisible(state, x => x.IsPaletteEditorVisible)
                                            .Children(
                                                new TextBlock()
                                                    .Text(L("Recent colors")),

                                                new ColorPalette()
                                                    .Margin(-6, 0)
                                                    .Colors(state.RecentColors)
                                                    .SelectedColor(state, x => x.SelectedColor, BindingMode.OneWay)
                                                    .OnColorSelected(c => state.SelectedColor = c),

                                                new TextBlock()
                                                    .Text(L("Custom colors")),

                                                new ColorPalette()
                                                    .Margin(-6, 0)
                                                    .CanAddColor(true)
                                                    .Colors(state.CustomColors)
                                                    .OnColorAdded(c => state.AddCustomColor(c))
                                                    .OnColorRemoved(state.OnColorRemoved)
                                                    .ColorToAdd(state, x => x.SelectedColor, BindingMode.OneWay)
                                                    .SelectedColor(state, x => x.SelectedColor, BindingMode.OneWay)
                                                    .OnColorSelected(c => state.SelectedColor = c)
                                            )
                                    ),
                                new TabItem() //HEX EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("Hex")
                                    .Content(
                                        new TextBox()
                                            .VerticalAlignment(VerticalAlignment.Top)
                                            .Text(state, x => x.HexValue, BindingMode.TwoWay)
                                            .OnKeyDown(args =>
                                            {
                                                if (args.Key == Key.Enter) state.ApplyHexInput();
                                                if (args.Key == Key.Escape) state.CancelHexInput();
                                            })
                                            .OnLostFocus(args => state.ApplyHexInput())
                                    ),
                                new TabItem() // HSV EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("HSV")
                                    .Content(
                                        new StackPanel()
                                            .Children(
                                                new SliderEx().Label(L("Hue")).Minimum(0).Maximum(360)
                                                    .Value(state, x => x.HsvHPart, BindingMode.TwoWay),

                                                new SliderEx().Label(L("Saturation")).Minimum(0).Maximum(100)
                                                    .Value(state, x => x.HsvSPart, BindingMode.TwoWay),

                                                new SliderEx().Label(L("Value")).Minimum(0).Maximum(100)
                                                    .Value(state, x => x.HsvVPart, BindingMode.TwoWay)
                                            )
                                    ),
                                new TabItem() // RGB EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("RGB")
                                    .Content(
                                        new StackPanel()
                                            .Children(
                                                new SliderEx()
                                                    .Label(L("Red"))
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(state, x => x.RedColorPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Label(L("Green"))
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(state, x => x.GreenColorPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Label(L("Blue"))
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(state, x => x.BlueColorPart, BindingMode.TwoWay)
                                            )
                                    )
                            )
                    ),

                BuildPaletteMenuButton(state)
            );
    }

    // All palette-library actions live under one compact menu button in the top-right of the
    // tab strip (Row 2). The flyout opens downward (room below) so nothing clips; the saved-palette
    // list and delete list are submenus so an arbitrarily long library never overflows the popup.
    private static Control BuildPaletteMenuButton(State state) =>
        new Button()
            .Row(2)
            .HorizontalAlignment(HorizontalAlignment.Right)
            .VerticalAlignment(VerticalAlignment.Top)
            .Margin(0, 14, 12, 0)
            .Width(30)
            .Height(30)
            .Padding(0)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.ButtonBackgroundBrush)
            .Content(new TextBlock()
                .Text("⋯")
                .FontSize(18)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center)
                .Foreground(StaticResources.Brushes.IconForegroundBrush))
            .Flyout(
                new MenuFlyout()
                    .Placement(PlacementMode.Bottom)
                    .ItemsSource(new Control[]
                    {
                        new MenuItem().Header(L("Load palette"))
                            .ItemsSource(state.LoadMenuItems),
                        new MenuItem().Header(L("Save current palette…"))
                            .OnClick(e => _ = state.SaveCurrentPaletteAsync()),
                        new MenuItem().Header(L("Delete saved palette"))
                            .ItemsSource(state.DeleteMenuItems),
                        new Separator(),
                        new MenuItem().Header(L("Import from file…"))
                            .OnClick(e => _ = state.ImportPaletteAsync()),
                        new MenuItem().Header(L("Export to file…"))
                            .OnClick(e => _ = state.ExportPaletteAsync()),
                        new MenuItem().Header(L("Load from Lospec…"))
                            .OnClick(e => _ = state.LoadFromLospecAsync()),
                    })
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IPaletteService _paletteService;
        private readonly IDrawingService _drawingService;
        private readonly IDialogService _dialogService;
        private SKColor _previousColor;
        private bool _isUpdatingEditors;
        private bool _isSyncingSelectedColor;
        private string? _currentPaletteName;

        [ObservableProperty]
        public partial SKColor SelectedColor { get; set; }

        [ObservableProperty]
        public partial int ColorTypeIndex { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPaletteEditorVisible))]
        public partial bool EditorMode { get; set; }

        [ObservableProperty]
        public partial double RedColorPart { get; set; }

        [ObservableProperty]
        public partial double GreenColorPart { get; set; }

        [ObservableProperty]
        public partial double BlueColorPart { get; set; }

        [ObservableProperty]
        public partial double HsvHPart { get; set; }

        [ObservableProperty]
        public partial double HsvSPart { get; set; }

        [ObservableProperty]
        public partial double HsvVPart { get; set; }

        [ObservableProperty]
        public partial string HexValue { get; set; } = string.Empty;

        public ObservableCollection<SKColor> CustomColors { get; } = [];
        public ObservableCollection<SKColor> RecentColors { get; } = [];

        // Saved-palette library rendered as upward-opening menus (kept as menu items so the list
        // can never be clipped at the bottom of the color-picker popup — see #palette feedback).
        public ObservableCollection<Control> LoadMenuItems { get; } = [];
        public ObservableCollection<Control> DeleteMenuItems { get; } = [];
        public bool IsPaletteEditorVisible => !EditorMode;
        public bool IsEyedropperSelected => _appState.ToolsState.CurrentToolKey == "EyedropperTool";

        public State(AppState appState, IMessenger messenger, IPaletteService paletteService, IDrawingService drawingService, IDialogService dialogService)
        {
            _appState = appState;
            _paletteService = paletteService;
            _drawingService = drawingService;
            _dialogService = dialogService;

            SelectedColor = _appState.SpriteEditorState.CurrentColor;
            LoadColors();
            RefreshSavedPalettes();
            UpdateEditors();
            _previousColor = SelectedColor;

            _appState.ToolsState.WatchFor(x => x.CurrentToolKey, () => OnPropertyChanged(nameof(IsEyedropperSelected)));
            _appState.SpriteEditorState.WatchFor(x => x.CurrentColor, OnDrawingStateColorChanged);
            messenger.Register<DrawingServiceOnDrawnMessage>(this, DrawingServiceDrawn);
            _paletteService.PaletteChanged += PaletteService_PaletteChanged;
            _paletteService.SavedPalettesChanged += (_, _) => RefreshSavedPalettes();
        }

        partial void OnSelectedColorChanged(SKColor value)
        {
            if (_isSyncingSelectedColor)
                return;

            if (_appState.SpriteEditorState.CurrentColor != value)
            {
                _drawingService.SetCurrentColor(value);
            }

            _previousColor = value;
            UpdateEditors();
        }

        partial void OnRedColorPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = new SKColor(ToByte(value), ToByte(GreenColorPart), ToByte(BlueColorPart));
        }

        partial void OnGreenColorPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = new SKColor(ToByte(RedColorPart), ToByte(value), ToByte(BlueColorPart));
        }

        partial void OnBlueColorPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = new SKColor(ToByte(RedColorPart), ToByte(GreenColorPart), ToByte(value));
        }

        partial void OnHsvHPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = SKColor.FromHsv((float)value, (float)HsvSPart, (float)HsvVPart);
        }

        partial void OnHsvSPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = SKColor.FromHsv((float)HsvHPart, (float)value, (float)HsvVPart);
        }

        partial void OnHsvVPartChanged(double value)
        {
            if (_isUpdatingEditors)
                return;

            SelectedColor = SKColor.FromHsv((float)HsvHPart, (float)HsvSPart, (float)value);
        }

        public void AddCustomColor(SKColor color)
        {
            _paletteService.InsertColor(nameof(IPaletteService.CustomPalette), color, -1);
        }

        public void OnColorRemoved(SKColor color)
        {
            var colorToRemove = color == default ? SelectedColor : color;
            if (colorToRemove != default)
            {
                _paletteService.RemoveColor(nameof(IPaletteService.CustomPalette), colorToRemove);
            }
        }

        private void LoadNamedPalette(string name)
        {
            _currentPaletteName = name;
            _paletteService.LoadSavedPalette(name);
        }

        public async Task SaveCurrentPaletteAsync()
        {
            var name = await _dialogService.ShowInputDialogAsync(
                L("Palette name"), L("Save palette"), _currentPaletteName ?? L("My palette"));
            if (string.IsNullOrWhiteSpace(name))
                return;

            _currentPaletteName = name.Trim();
            _paletteService.SaveCurrentPaletteAs(_currentPaletteName);
        }

        public async Task DeleteNamedPaletteAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            var confirmed = await _dialogService.ShowYesNoDialog(
                string.Format(L("Delete palette \"{0}\"?"), name), L("Delete palette"));
            if (!confirmed)
                return;

            if (_currentPaletteName == name)
                _currentPaletteName = null;

            _paletteService.DeleteSavedPalette(name);
        }

        public Task ImportPaletteAsync() => _paletteService.ImportPaletteFromFileAsync();

        public Task ExportPaletteAsync() => _paletteService.ExportPaletteToFileAsync(_currentPaletteName ?? "palette");

        public async Task LoadFromLospecAsync()
        {
            var slug = await _dialogService.ShowInputDialogAsync(
                L("Lospec palette name or URL"), L("Load from Lospec"));
            if (string.IsNullOrWhiteSpace(slug))
                return;

            var loaded = await _paletteService.ImportPaletteFromLospecAsync(slug.Trim());
            if (!loaded)
                _dialogService.Alert(L("Could not load palette from Lospec"), L("Load from Lospec"));
        }

        private void RefreshSavedPalettes()
        {
            LoadMenuItems.Clear();
            DeleteMenuItems.Clear();

            var names = _paletteService.GetSavedPaletteNames();
            if (names.Count == 0)
            {
                LoadMenuItems.Add(new MenuItem().Header(L("(no saved palettes)")).With(m => m.IsEnabled = false));
                DeleteMenuItems.Add(new MenuItem().Header(L("(no saved palettes)")).With(m => m.IsEnabled = false));
                return;
            }

            foreach (var name in names)
            {
                var captured = name;
                LoadMenuItems.Add(new MenuItem().Header(captured).OnClick(_ => LoadNamedPalette(captured)));
                DeleteMenuItems.Add(new MenuItem().Header(captured).OnClick(e => _ = DeleteNamedPaletteAsync(captured)));
            }
        }

        public void ApplyHexInput()
        {
            if (SKColor.TryParse(HexValue, out var parsedColor))
            {
                HexValue = FormatHex(parsedColor);
                SelectedColor = parsedColor;
                return;
            }

            CancelHexInput();
        }

        public void CancelHexInput()
        {
            HexValue = FormatHex(SelectedColor);
        }

        private void OnDrawingStateColorChanged()
        {
            var currentColor = _appState.SpriteEditorState.CurrentColor;
            if (currentColor.Equals(_previousColor))
                return;

            _isSyncingSelectedColor = true;
            SelectedColor = currentColor;
            _isSyncingSelectedColor = false;

            _previousColor = currentColor;
            UpdateEditors();
        }

        private void PaletteService_PaletteChanged(object? sender, Primitives.Palette.PaletteChangedEventArgs e)
        {
            LoadColors(e.PaletteName);
        }

        private void DrawingServiceDrawn(DrawingServiceOnDrawnMessage _)
        {
            if (RecentColors.Count > 0 && RecentColors[0] == SelectedColor)
                return;

            _paletteService.InsertColor(nameof(IPaletteService.RecentPalette), SelectedColor, 0);
            LoadPalette(RecentColors, _paletteService.RecentPalette);
        }

        private void LoadColors(string paletteName = default!)
        {
            if (paletteName == default || paletteName == nameof(IPaletteService.CustomPalette))
            {
                LoadPalette(CustomColors, _paletteService.CustomPalette);
            }

            if (paletteName == default || paletteName == nameof(IPaletteService.RecentPalette))
            {
                LoadPalette(RecentColors, _paletteService.RecentPalette);
            }
        }

        private void LoadPalette(ObservableCollection<SKColor> targetCollection, IEnumerable<SKColor> source)
        {
            targetCollection.Clear();
            foreach (var color in source)
            {
                targetCollection.Add(color);
            }
        }

        private void UpdateEditors()
        {
            var value = SelectedColor;

            _isUpdatingEditors = true;
            RedColorPart = value.Red;
            GreenColorPart = value.Green;
            BlueColorPart = value.Blue;

            value.ToHsv(out var hsvHPart, out var hsvSPart, out var hsvVPart);
            HsvHPart = Math.Round(hsvHPart);
            HsvSPart = Math.Round(hsvSPart);
            HsvVPart = Math.Round(hsvVPart);

            HexValue = FormatHex(value);
            _isUpdatingEditors = false;
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static string FormatHex(SKColor color)
        {
            return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
        }
    }

    public enum ColorPickerColorType : byte
    {
        Hex = 1,
        Hsv = 2,
        Rgb = 3,
    }
}