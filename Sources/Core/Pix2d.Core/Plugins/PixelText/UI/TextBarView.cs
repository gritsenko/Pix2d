using System.Collections.ObjectModel;
using System.Globalization;
using Pix2d.Abstract.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Pix2d.Plugins.PixelText;
using Pix2d.Primitives;
using Pix2d.State;
using Pix2d.UI.Resources;

namespace Pix2d.Views.Text;

public class TextBarView : ComponentBase
{
    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(new Control[]
            {
                new Button() //ENTER TEXT FLYOUT
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout()
                            .Placement(PlacementMode.Bottom);
                        b.Click += (s, e) => flyout.ShowAt(b);

                        flyout.Content = new Grid()
                            .Children(
                                new TextBox()
                                    .Watermark("Enter text")
                                    .Text(() => Text, v => Text = (string)v!)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .AcceptsReturn(false)
                                    .MinWidth(150)
                            );
                    })
                    .Content("\xF741"),
                new Button() //FONT PROPERTIES FLYOUT
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout()
                            .Placement(PlacementMode.Bottom)
                            .Content(new StackPanel()
                                .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                                .Orientation(Orientation.Horizontal)
                                .Children(new Control[]
                                    {
                                        new TextBlock()
                                            .Margin(8, 0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font"),

                                        new ComboBox()
                                            .Width(180)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .ItemsSource(Fonts)
                                            .SelectedItem(() => SelectedFont!, v => 
                                            {
                                                if (v is FontItemViewModel font)
                                                    SelectedFont = font;
                                            })
#pragma warning disable CS8603
                                            .ItemTemplate((FontItemViewModel? item) => CreateFontItemTemplate(item!)),
#pragma warning restore CS8603

                                        new TextBlock()
                                            .Margin(8, 0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font size"),

                                        new NumericUpDown()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                            .Increment(1)
                                            .Value(() => FontSize, v => FontSize = (int)v!),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DD")
                                            .IsChecked(() => IsBold, v => IsBold = (bool)v!),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DB")
                                            .IsChecked(() => IsItalic, v => IsItalic = (bool)v!),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8D2")
                                            .IsChecked(() => IsAliased, v => IsAliased = (bool)v!),
                                    }
                                )
                            );
                        b.Click += (s, e) => flyout.ShowAt(b);
                    })
                    .Content("\xE8D2"),

                new Button()
                    .OnClick(_ => OnCancelButtonClicked())
                    .IsEnabled(() => !string.IsNullOrEmpty(Text))
                    .With(ButtonStyle)
                    .Content("\xE711"),

                new Button()
                    .OnClick(_ => OnApplyButtonClicked())
                    .IsEnabled(() => !string.IsNullOrEmpty(Text))
                    .With(ButtonStyle)
                    .Content("\xE73E")
            });

    private void ButtonStyle(Button b)
    {
        b.Classes("AppBarButton")
            .Width(48)
            .Height(48)
            .FontSize(22)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
        {
            b.ToolTip(pc.Tooltip);
        }
    }

    [Inject] public IFontService FontService { get; set; } = null!;
    [Inject] public AppState AppState { get; set; } = null!;

    private PixelTextTool? _pixelTextTool 
    { 
        get 
        {
            var tool = AppState.ToolsState.Tools
                .FirstOrDefault(x => x.ToolType == typeof(PixelTextTool));
            return tool?.ToolInstance as PixelTextTool;
        }
    }

    public string Text
    {
        get => _pixelTextTool?.Text ?? "";
        set
        {
            if (_pixelTextTool != null)
            {
                _pixelTextTool.Text = value;
                OnPropertyChanged();
                StateHasChanged();
            }
        }
    }

    public bool IsBold
    {
        get => _pixelTextTool?.IsBold ?? false;
        set
        {
            if (_pixelTextTool != null)
                _pixelTextTool.IsBold = value;
        }
    }


    public bool IsItalic
    {
        get => _pixelTextTool?.IsItalic ?? false;
        set
        {
            if (_pixelTextTool != null)
                _pixelTextTool.IsItalic = value;
        }
    }


    public bool IsAliased
    {
        get => _pixelTextTool?.IsAliased ?? false;
        set
        {
            if (_pixelTextTool != null)
                _pixelTextTool.IsAliased = value;
        }
    }

    public FontItemViewModel? SelectedFont
    {
        get
        {
            var selectedFontName = _pixelTextTool?.SelectedFont;
            if (selectedFontName == null)
                return null;
            
            var result = Fonts.FirstOrDefault(x =>
                x.Name.Equals(selectedFontName, StringComparison.InvariantCultureIgnoreCase))!;
            return result;
        }
        set
        {
            if (_pixelTextTool != null)
            {
                _pixelTextTool.SelectedFont = value?.Name ?? "";
                OnPropertyChanged();
            }
        }
    }

    public int FontSize
    {
        get => (int)(_pixelTextTool?.FontSize ?? 14);
        set
        {
            if (_pixelTextTool != null)
                _pixelTextTool.FontSize = value;
        }
    }

    private void OnApplyButtonClicked()
    {
        OnTextApplied();
        Logger.Log("Apply text");
        Text = "";
    }

    private void OnCancelButtonClicked()
    {
        Logger.Log("Cancel text");
        Text = "";
    }

    public ObservableCollection<FontItemViewModel> Fonts { get; set; } = new();

    protected override void OnAfterInitialized()
    {
        LoadFonts();
    }

    private async void LoadFonts()
    {
        var fonts = await FontService.GetAvailableFontNamesAsync();
        foreach (string font in fonts)
        {
            Fonts.Add(new FontItemViewModel(font));
            // Debug.WriteLine(string.Format("Font: {0}", font));
        }

        if (_pixelTextTool != null)
        {
            SelectedFont =
                Fonts.FirstOrDefault(x => x.Name.Equals("Arial", StringComparison.InvariantCultureIgnoreCase)) ??
                Fonts.FirstOrDefault();
        }

        StateHasChanged();
    }

    protected virtual void OnTextApplied()
    {
        if (_pixelTextTool != null && AppState.ToolsState.CurrentTool?.ToolInstance == _pixelTextTool)
        {
            _pixelTextTool.ApplyText(Text);
        }
    }

    private TextBlock CreateFontItemTemplate(FontItemViewModel? item)
    {
        return new TextBlock().Width(150).Text(item?.Name ?? "")!;
    }

    public class FontItemViewModel(string fontName)
    {
        public string Name { get; set; } = fontName;
    }
}