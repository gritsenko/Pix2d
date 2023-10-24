using Avalonia.Controls.Shapes;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class LockedFunctionView : ViewBase
{
    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<LockedFunctionView, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<LockedFunctionView, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = default;
    public Control Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }


    protected override object Build() =>
        new Grid()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Children(
                new ContentControl()
                    .Content(@ContentProperty, BindingMode.OneWay)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .VerticalContentAlignment(VerticalAlignment.Stretch),

                new Rectangle()
                    .Fill("#10ffffff".ToColor().ToBrush()),

                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Margin(0,4,4,0)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(12).Text("\xe1f7")
            );
}