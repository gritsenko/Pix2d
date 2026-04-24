using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Platform;
using Pix2d.Plugins.PixelText;
using Pix2d.UI.Resources;

namespace Pix2d.Views.Text;

public partial class TextBarView(IFontService fontService) : ViewBase
{
    private const double FontComboBoxWidth = 220;
    private readonly State _state = new(fontService);

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(new Control[]
            {
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout().Placement(PlacementMode.Bottom);
                        b.Click += (_, _) => flyout.ShowAt(b);

                        flyout.Content = new Grid()
                            .Children(
                                new TextBox()
                                    .With(t => t.PlaceholderText = "Enter text")
                                    .Text(_state, x => x.Text, BindingMode.TwoWay)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .AcceptsReturn(false)
                                    .MinWidth(150)
                            );
                    })
                    .Content("\xF741"),

                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout()
                            .Placement(PlacementMode.Bottom)
                            .Content(
                                new StackPanel()
                                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                                    .Orientation(Orientation.Horizontal)
                                    .Children(new Control[]
                                    {
                                        new TextBlock()
                                            .Margin(8, 0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font"),

                                        new ComboBox()
                                            .Width(FontComboBoxWidth)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                            .ItemsSource(_state.Fonts)
                                            .SelectedItem(_state, x => x.SelectedFont, BindingMode.TwoWay)
                                            .ItemTemplate((State.FontItemViewModel? item) => CreateFontItemTemplate(item))
                                            .SelectionBoxItemTemplate(new Avalonia.Controls.Templates.FuncDataTemplate<State.FontItemViewModel?>((item, _) => CreateFontItemTemplate(item))),

                                        new TextBlock()
                                            .Margin(8, 0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font size"),

                                        new NumericUpDown()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .NumberFormat(new NumberFormatInfo { NumberDecimalDigits = 0 })
                                            .Increment(1)
                                            .Value(_state, x => x.FontSize, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DD")
                                            .IsChecked(_state, x => x.IsBold, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DB")
                                            .IsChecked(_state, x => x.IsItalic, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8D2")
                                            .IsChecked(_state, x => x.IsAliased, BindingMode.TwoWay)
                                    })
                            );
                        b.Click += (_, _) => flyout.ShowAt(b);
                    })
                    .Content("\xE8D2"),

                new Button()
                    .OnClick(_ => _state.CancelText())
                    .IsEnabled(_state, x => x.CanApply)
                    .With(ButtonStyle)
                    .Content("\xE711"),

                new Button()
                    .OnClick(_ => _state.ApplyText())
                    .IsEnabled(_state, x => x.CanApply)
                    .With(ButtonStyle)
                    .Content("\xE73E")
            });

    private void ButtonStyle(Button button)
    {
        button.Classes("AppBarButton")
            .Width(48)
            .Height(48)
            .FontSize(22)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (button.Command is Pix2dCommand command)
            button.ToolTip_Tip(command.Tooltip);
    }

    protected override void OnAfterInitialized()
    {
        _state.EnsureFontsLoaded();
        _state.SetTool(DataContext as PixelTextTool);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
            _state.SetTool(change.NewValue as PixelTextTool);
    }

    private static TextBlock CreateFontItemTemplate(State.FontItemViewModel? item)
    {
        return new TextBlock()
            .Text(item?.Name ?? string.Empty)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .TextAlignment(TextAlignment.Left)
            .TextWrapping(Avalonia.Media.TextWrapping.NoWrap)
            .TextTrimming(Avalonia.Media.TextTrimming.CharacterEllipsis)!;
    }

    public sealed partial class State : ObservableObject
    {
        private const string DefaultFontName = "Arial";
        private readonly IFontService _fontService;
        private PixelTextTool? _tool;
        private bool _isSyncing;
        private bool _fontsLoaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanApply))]
        public partial string Text { get; set; } = string.Empty;

        [ObservableProperty]
        public partial FontItemViewModel? SelectedFont { get; set; }

        [ObservableProperty]
        public partial decimal FontSize { get; set; } = 14;

        [ObservableProperty]
        public partial bool IsBold { get; set; }

        [ObservableProperty]
        public partial bool IsItalic { get; set; }

        [ObservableProperty]
        public partial bool IsAliased { get; set; }

        public State(IFontService fontService)
        {
            _fontService = fontService;
            SelectedFont = new FontItemViewModel(DefaultFontName);
        }

        public ObservableCollection<FontItemViewModel> Fonts { get; } = [];

        public bool CanApply => !string.IsNullOrWhiteSpace(Text);

        public async void EnsureFontsLoaded()
        {
            if (_fontsLoaded)
                return;

            _fontsLoaded = true;
            var fonts = await _fontService.GetAvailableFontNamesAsync();
            foreach (var font in fonts)
            {
                Fonts.Add(new FontItemViewModel(font));
            }

            if (_tool == null)
            {
                SelectedFont = ResolveSelectedFont(SelectedFont?.Name);
                return;
            }

            SelectedFont = ResolveSelectedFont(_tool.SelectedFont);

            if (string.IsNullOrWhiteSpace(_tool.SelectedFont) && SelectedFont != null)
                _tool.SelectedFont = SelectedFont.Name;
        }

        public void SetTool(PixelTextTool? tool)
        {
            _tool = tool;

            if (_tool != null && string.IsNullOrWhiteSpace(_tool.SelectedFont) && SelectedFont != null)
                _tool.SelectedFont = SelectedFont.Name;

            SyncFromTool();
        }

        public void ApplyText()
        {
            if (_tool == null)
                return;

            Logger.Log("Apply text");
            _tool.ApplyText(Text);
            Text = string.Empty;
        }

        public void CancelText()
        {
            Logger.Log("Cancel text");
            Text = string.Empty;
        }

        partial void OnTextChanged(string value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.Text = value;
        }

        partial void OnSelectedFontChanged(FontItemViewModel? value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.SelectedFont = value?.Name ?? string.Empty;
        }

        partial void OnFontSizeChanged(decimal value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.FontSize = (float)value;
        }

        partial void OnIsBoldChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.IsBold = value;
        }

        partial void OnIsItalicChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.IsItalic = value;
        }

        partial void OnIsAliasedChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.IsAliased = value;
        }

        private void SyncFromTool()
        {
            _isSyncing = true;

            Text = _tool?.Text ?? string.Empty;
            FontSize = (decimal)(_tool?.FontSize ?? 14);
            IsBold = _tool?.IsBold ?? false;
            IsItalic = _tool?.IsItalic ?? false;
            IsAliased = _tool?.IsAliased ?? false;
            SelectedFont = ResolveSelectedFont(_tool?.SelectedFont);

            _isSyncing = false;
        }

        private FontItemViewModel? ResolveSelectedFont(string? fontName)
        {
            if (Fonts.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(fontName))
                    return new FontItemViewModel(fontName);

                return SelectedFont ?? new FontItemViewModel(DefaultFontName);
            }

            if (!string.IsNullOrWhiteSpace(fontName))
            {
                return Fonts.FirstOrDefault(x => x.Name.Equals(fontName, StringComparison.InvariantCultureIgnoreCase))
                    ?? Fonts.FirstOrDefault(x => x.Name.Equals(DefaultFontName, StringComparison.InvariantCultureIgnoreCase))
                    ?? Fonts.FirstOrDefault();
            }

            return Fonts.FirstOrDefault(x => x.Name.Equals(DefaultFontName, StringComparison.InvariantCultureIgnoreCase))
                ?? Fonts.FirstOrDefault();
        }

        public sealed class FontItemViewModel(string fontName)
        {
            public string Name { get; set; } = fontName;

            public override string ToString() => Name;
        }
    }
}