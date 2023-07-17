using System;
using System.Windows.Input;

namespace Pix2d.Shared;

public class AppButton : ViewBase
{
    public event EventHandler Click;

    public static readonly DirectProperty<AppButton, string> LabelProperty
        = AvaloniaProperty.RegisterDirect<AppButton, string>(nameof(Label), o => o.Label, (o, v) => o.Label = v);
    private string _label = "Label";
    public string Label
    {
        get => _label;
        set => SetAndRaise(LabelProperty, ref _label, value);
    }


    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<AppButton, IBrush>(nameof(Background));

    /// <summary>
    /// Command
    /// </summary>
    public static readonly DirectProperty<AppButton, ICommand> CommandProperty
        = AvaloniaProperty.RegisterDirect<AppButton, ICommand>(nameof(Command), o => o.Command, (o, v) => o.Command = v);
    private ICommand _command;
    public ICommand Command
    {
        get => _command;
        set => SetAndRaise(CommandProperty, ref _command, value);
    }

    /// <summary>
    /// Content
    /// </summary>
    public static readonly DirectProperty<AppButton, object> ContentProperty
        = AvaloniaProperty.RegisterDirect<AppButton, object>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private object _content;
    public object Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    /// <summary>
    /// Content
    /// </summary>
    public static readonly DirectProperty<AppButton, FontFamily> IconFontFamilyProperty
        = AvaloniaProperty.RegisterDirect<AppButton, FontFamily>(nameof(IconFontFamily), o => o.IconFontFamily, (o, v) => o.IconFontFamily = v);
    private FontFamily _iconFontFamily;
    public FontFamily IconFontFamily
    {
        get => _iconFontFamily;
        set => SetAndRaise(IconFontFamilyProperty, ref _iconFontFamily, value);
    }

    protected override object Build() =>
        new Button()
            .Command(CommandProperty)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .Padding(0)
            .Margin(0)
            .OnClick(args => Click?.Invoke(this, args))
            .Content(
                new Border()
                    .Child(
                        new Grid()
                            .Rows("24, Auto")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new ContentControl()
                                    .FontSize(16)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                                    .FontFamily(IconFontFamilyProperty)
                                    .Content(ContentProperty),

                                new TextBlock().Row(1)
                                    .Text(LabelProperty)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                            )
                    )
            );
}