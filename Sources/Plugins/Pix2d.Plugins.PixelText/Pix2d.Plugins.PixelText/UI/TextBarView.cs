using System.Collections.ObjectModel;
using System.Globalization;
using Pix2d.Abstract.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives;
using Pix2d.Resources;
using Pix2d.State;

namespace Pix2d.Views.Text;

public class TextBarView : ComponentBase {

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(new Control[] {
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
                                .Text(Bind(Text))
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
                                            .Margin(8,0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font"),

                                        new ComboBox()
                                            .Width(180)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .ItemsSource(Fonts)
                                            .SelectedItem(Bind(SelectedFont, BindingMode.TwoWay))
                                            .ItemTemplate(
                                                (FontItemViewModel item) => new TextBlock().Width(150).Text(item?.Name ?? "")),

                                        new TextBlock()
                                            .Margin(8,0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font size"),

                                        new NumericUpDown()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                            .Increment(1)
                                            .Value(FontSize, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DD")
                                            .IsChecked(IsBold, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DB")
                                            .IsChecked(IsItalic, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8D2")
                                            .IsChecked(IsAliased, BindingMode.TwoWay),
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

    private void ButtonStyle(Button b) {
        b.Classes("AppBarButton")
            .Width(48)
            .Height(48)
            .FontSize(22)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc) {
            b.ToolTip(pc.Tooltip);
        }
    }
    [Inject] public IFontService FontService { get; set; } = null!;
    [Inject] public AppState AppState { get; set; } = null!;
    [Inject] public IToolService ToolService { get; set; } = null!;

    private PixelTextTool? _pixelTextTool = null!;
    public string Text
    {
        get => _pixelTextTool?.Text ?? "";
        set
        {
            _pixelTextTool.Text = value;
            OnPropertyChanged();
            StateHasChanged();
        }
    }

    public bool IsBold
    {
        get => _pixelTextTool?.IsBold ?? false;
        set => _pixelTextTool.IsBold = value;
    }


    public bool IsItalic
    {
        get => _pixelTextTool?.IsItalic ?? false;
        set => _pixelTextTool.IsItalic = value;
    }


    public bool IsAliased
    {
        get => _pixelTextTool?.IsAliased ?? false;
        set => _pixelTextTool.IsAliased = value;
    }

    public FontItemViewModel? SelectedFont
    {
        get => Fonts.FirstOrDefault(x => x.Name.Equals(_pixelTextTool?.SelectedFont, StringComparison.InvariantCultureIgnoreCase));
        set
        {
            _pixelTextTool.SelectedFont = value?.Name ?? "";
            OnPropertyChanged();
        }
    }

    public int FontSize
    {
        get => (int)(_pixelTextTool?.FontSize ?? 14);
        set => _pixelTextTool.FontSize = value;
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

    protected override async void OnAfterInitialized()
    {
        if(ToolService.GetToolByKey(nameof(PixelTextTool)) is PixelTextTool pixelTextTool)
        {
            _pixelTextTool = pixelTextTool;
        }

        await LoadFonts();
    }

    private async Task LoadFonts()
    {
        var fonts = await FontService.GetAvailableFontNamesAsync();
        foreach (string font in fonts)
        {
            Fonts.Add(new FontItemViewModel(font));
            // Debug.WriteLine(string.Format("Font: {0}", font));
        }

        SelectedFont =
            Fonts.FirstOrDefault(x => x.Name.Equals("Arial", StringComparison.InvariantCultureIgnoreCase)) ??
            Fonts.FirstOrDefault();

        StateHasChanged();
    }

    protected virtual void OnTextApplied()
    {
        var toolKey = AppState.UiState.CurrentToolKey;
        if (toolKey == _pixelTextTool.Key)
        {
            _pixelTextTool.ApplyText(Text);
        }
    }

    public class FontItemViewModel
    {
        public FontItemViewModel(string fontName)
        {
            Name = fontName;
        }

        public string Name { get; set; }
    }
}