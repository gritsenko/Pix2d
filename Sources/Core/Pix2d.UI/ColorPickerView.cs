using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;
using Mvvm;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI;

public class ColorPickerView : LocalizedComponentBase
{
    protected override object Build()
    {
        //var vm = this;
        //DataContext = this;

        return new Grid().Width(236)
            .Rows("140, Auto, *")
            .Children(
                new Pix2dColorPicker()
                    .Margin(10)
                    .Color(() => SelectedColor, v => SelectedColor = v),

                //new Grid().Row(1)
                //    .Cols("Auto, Auto, *")
                //    .Margin(10, 0, 10, 0)
                //    .Children(
                //        new ToggleButton() //Eyedropper
                //            .Width(38)
                //            .Height(38)
                //            .Background(Brushes.Transparent)
                //            .FontSize(20)
                //            .Padding(-1)
                //            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                //            .Content("\xEF3C"),

                //        new ToggleButton().Col(1) //Color editors
                //            .Width(38)
                //            .Height(38)
                //            .FontSize(20)
                //            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                //            .IsChecked(Bind(@vm.EditorMode, BindingMode.TwoWay))
                //            .Background(Brushes.Transparent)
                //            .Padding(0)
                //            .Content("\xE9E9")
                //    ),
                new Border().Row(2)
                    .Margin(8, 8, 8, 16)
                    .MinHeight(100)
                    //.IsVisible(@vm.EditorMode)
                    .Child(
                        new TabControl()
                            .SelectedIndex(() => ColorTypeIndex, v => ColorTypeIndex = v)
                            .Items(
                                new TabItem() //PALETTE EDITOR
                                    .Foreground(Brushes.White)
                                    .Header(L("List"))
                                    .Content(
                                        new StackPanel().Row(2)
                                            .IsVisible(() => !EditorMode)
                                            .Children(
                                                new TextBlock()
                                                    .Text(L("Recent colors")),
                                                new ColorPalette().Row(1)
                                                    .Margin(-6, 0)
                                                    .Colors(() => RecentColors)
                                                    .OnColorSelected()
                                                    .SelectColorCommand(() => SetColorCommand),
                                                new TextBlock()
                                                    .Text(L("Custom colors")),
                                                new ColorPalette().Row(1)
                                                    .Margin(-6, 0)
                                                    .Colors(() => CustomColors)
                                                    .CanAddColor(true)
                                                    .AddColorCommand(() => OnAddColorCommand)
                                                    .RemoveColorCommand(() => OnRemoveColorCommand)
                                                    .ColorToAdd(() => SelectedColor)
                                                    .SelectColorCommand(() => SetColorCommand)
                                            )
                                    ),
                                new TabItem() //HEX EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("Hex")
                                    .Content(
                                        new TextBox()
                                            .VerticalAlignment(VerticalAlignment.Top)
                                            .Text(() => HexValue, v => HexValue = v)
                                            .OnKeyDown(args =>
                                            {
                                                if (args.Key == Key.Enter) ApplyHexInput();
                                                if (args.Key == Key.Escape) CancelHexInput();
                                            })
                                            .OnLostFocus(args => ApplyHexInput())
                                    ),
                                new TabItem() // HSV EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("HSV")
                                    .Content(
                                        new StackPanel()
                                            .Children(
                                                new SliderEx()
                                                    .Label(L("Hue"))
                                                    .Minimum(0)
                                                    .Maximum(360)
                                                    .Value(() => HsvHPart, v => HsvHPart = (float)v),
                                                new SliderEx()
                                                    .Label(L("Saturation"))
                                                    .Minimum(0)
                                                    .Maximum(100)
                                                    .Value(() => HsvSPart, v => HsvSPart = (float)v),
                                                new SliderEx()
                                                    .Label(L("Value"))
                                                    .Minimum(0)
                                                    .Maximum(100)
                                                    .Value(() => HsvVPart, v => HsvVPart = (float)v)
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
                                                    .Value(() => RedColorPart, v => RedColorPart = (byte)v),
                                                new SliderEx()
                                                    .Label(L("Green"))
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(() => GreenColorPart, v => GreenColorPart = (byte)v),
                                                new SliderEx()
                                                    .Label(L("Blue"))
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(() => BlueColorPart, v => BlueColorPart = (byte)v)
                                            )
                                    )
                            )
                    )
            );
    }

    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private IPaletteService PaletteService { get; set; } = null!;
    [Inject] private IDrawingService DrawingService { get; set; } = null!;

    private ObservableCollection<SKColor> CustomColors { get; set; } = [];
    private ObservableCollection<SKColor> RecentColors { get; set; } = [];

    private SKColor _previousColor;

    private byte _rgbRPart;
    private byte _rgbGPart;
    private byte _rgbBPart;

    private float _hsvHPart;
    private float _hsvSPart;
    private float _hsvVPart;

    private string _hexValue;

    // Because of double-way binding, when changing one of the HSV values may change others due to
    // color value conversion to RGB and rounding. To prevent such unexpected behaviour we add this
    // guard.
    private bool _isUsingHsvControls;

    public SKColor SelectedColor
    {
        get => AppState.SpriteEditorState.CurrentColor;
        set
        {
            DrawingService.SetCurrentColor(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedColorBrush));
            UpdateEditors();
            StateHasChanged();
        }
    }

    public Brush SelectedColorBrush => SelectedColor.ToBrush();

    public ColorPickerColorType ColorType { get; set; }

    public int ColorTypeIndex
    {
        get => (int)ColorType;
        set
        {
            ColorType = (ColorPickerColorType)value;
            UpdateEditors();
        }
    }

    public bool EditorMode { get; set; }
    public SKColor PreviousColor { get; set; }

    #region RGB

    public byte RedColorPart
    {
        get => _rgbRPart;
        set
        {
            if (value == _rgbRPart)
                return;

            _rgbRPart = value;
            SelectedColor = new SKColor(value, _rgbGPart, _rgbBPart);
        }
    }

    public byte GreenColorPart
    {
        get => _rgbGPart;
        set
        {
            if (value == _rgbGPart)
                return;

            _rgbGPart = value;
            SelectedColor = new SKColor(_rgbRPart, value, _rgbBPart);
        }
    }

    public byte BlueColorPart
    {
        get => _rgbBPart;
        set
        {
            if (value == _rgbBPart)
                return;

            _rgbBPart = value;
            SelectedColor = new SKColor(_rgbRPart, _rgbGPart, value);
        }
    }

    #endregion

    #region HSV

    public float HsvHPart
    {
        get => _hsvHPart;
        set
        {
            if (Math.Abs(value - _hsvHPart) < float.Epsilon)
                return;

            _isUsingHsvControls = true;
            _hsvHPart = value;
            SelectedColor = SKColor.FromHsv(value, _hsvSPart, _hsvVPart);
            _isUsingHsvControls = false;
        }
    }

    public float HsvSPart
    {
        get => _hsvSPart;
        set
        {
            if (Math.Abs(value - _hsvSPart) < float.Epsilon)
                return;

            _isUsingHsvControls = true;
            _hsvSPart = value;
            SelectedColor = SKColor.FromHsv(_hsvHPart, value, _hsvVPart);
            _isUsingHsvControls = false;
        }
    }

    public float HsvVPart
    {
        get => _hsvVPart;
        set
        {
            if (Math.Abs(value - _hsvVPart) < float.Epsilon)
                return;

            _isUsingHsvControls = true;
            _hsvVPart = value;
            SelectedColor = SKColor.FromHsv(_hsvHPart, _hsvSPart, value);
            _isUsingHsvControls = false;
        }
    }

    #endregion

    public string HexValue
    {
        get => _hexValue;
        set => _hexValue = value;
    }


    public bool IsEyedropperSelected => AppState?.ToolsState.CurrentToolKey == "EyedropperTool";

    public ICommand OnAddColorCommand => new RelayCommand<SKColor>((c) =>
    {
        PaletteService.InsertColor(nameof(PaletteService.CustomPalette), c, -1);
    });

    public ICommand OnRemoveColorCommand => new RelayCommand<SKColor>((c) =>
    {
        if (c == default)
        {
            c = SelectedColor;
        }

        if (c != default)
        {
            PaletteService.RemoveColor(nameof(PaletteService.CustomPalette), c);
        }
    });

    public ICommand SetColorCommand => new RelayCommand<SKColor>(c => { SelectedColor = c; }, c => true);

    protected override void OnAfterInitialized()
    {
        LoadColors();

        AppState.SpriteEditorState.WatchFor(x => x.CurrentColor, OnDrawingStateColorChanged);

        Messenger.Register<DrawingServiceOnDrawnMessage>(this, DrawingServiceDrawn);
        Messenger.Register<CurrentToolChangedMessage>(this, OnCurrentToolChanged);

        PaletteService.PaletteChanged += PaletteService_PaletteChanged;
    }

    private void OnDrawingStateColorChanged()
    {
        if (SelectedColor.Equals(_previousColor))
            return;

        UpdateEditors();
        _previousColor = SelectedColor;
        OnPropertyChanged(nameof(SelectedColor));
    }

    private void UpdateEditors()
    {
        var value = SelectedColor;

        if (ColorType == ColorPickerColorType.Rgb)
        {
            _rgbRPart = value.Red;
            _rgbGPart = value.Green;
            _rgbBPart = value.Blue;
            OnPropertyChanged(nameof(RedColorPart));
            OnPropertyChanged(nameof(GreenColorPart));
            OnPropertyChanged(nameof(BlueColorPart));
        }


        if (ColorType == ColorPickerColorType.Hsv && !_isUsingHsvControls)
        {
            value.ToHsv(out var hsvHPart, out var hsvSPart, out var hsvVPart);
            _hsvHPart = (float)Math.Round(hsvHPart);
            _hsvSPart = (float)Math.Round(hsvSPart);
            _hsvVPart = (float)Math.Round(hsvVPart);

            OnPropertyChanged(nameof(HsvHPart));
            OnPropertyChanged(nameof(HsvSPart));
            OnPropertyChanged(nameof(HsvVPart));
        }

        if (ColorType == ColorPickerColorType.Hex)
        {
            _hexValue = $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
            OnPropertyChanged(nameof(HexValue));
        }
    }

    private void OnCurrentToolChanged(CurrentToolChangedMessage obj)
    {
        OnPropertyChanged(nameof(IsEyedropperSelected));
    }

    private void PaletteService_PaletteChanged(object sender, Primitives.Palette.PaletteChangedEventArgs e)
    {
        LoadColors(e.PaletteName);
    }

    private void DrawingServiceDrawn(DrawingServiceOnDrawnMessage drawingServiceOnDrawnMessage)
    {
        if (RecentColors[0] == SelectedColor)
            return;

        PaletteService.InsertColor(nameof(PaletteService.RecentPalette), SelectedColor, 0);

        var recentColors = PaletteService.RecentPalette;
        RecentColors.Clear();
        foreach (var c in recentColors)
            RecentColors.Add(c);
    }

    private void LoadColors(string paletteName = default)
    {
        if (paletteName == default || paletteName == nameof(PaletteService.CustomPalette))
            LoadPalette(CustomColors, PaletteService.CustomPalette);

        if (paletteName == default || paletteName == nameof(PaletteService.RecentPalette))
            LoadPalette(RecentColors, PaletteService.RecentPalette);
        //PreviousColor = SKColor.FromHsv(0, 0, 50);
    }
    void LoadPalette(ObservableCollection<SKColor> targetColl, IEnumerable<SKColor> src)
    {
        targetColl.Clear();
        foreach (var customColor in src)
            targetColl.Add(customColor);
    }

    public void ApplyHexInput()
    {

        if (SKColor.TryParse(_hexValue, out var parsedColor))
        {
            _hexValue = $"#{parsedColor.Red:X2}{parsedColor.Green:X2}{parsedColor.Blue:X2}";
            SelectedColor = parsedColor;
            UpdateEditors();
        }
        else
        {
            CancelHexInput();
        }
    }

    public void CancelHexInput()
    {
        HexValue = SelectedColor.ToString();
        UpdateEditors();
    }

    public enum ColorPickerColorType : byte
    {
        Hex = 1,
        Hsv = 2,
        Rgb = 3,
    }
}