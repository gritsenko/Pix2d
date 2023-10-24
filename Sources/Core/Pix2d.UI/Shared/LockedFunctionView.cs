using System.ComponentModel;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class LockedFunctionView : ComponentBase
{
    private Control? _content;

    public Control? Content
    {
        get => _content;
        set
        {
            _content = value;
            OnPropertyChanged();
        }
    }

    protected override object Build() =>
        new Grid().Rows("*").Cols("*").Children(
            new ContentControl().Content(@Content),
            new TextBlock().Row(0).Col(0)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center)
                .Background("#55000000".ToColor().ToBrush()).FontFamily(StaticResources.Fonts.IconFontSegoe).FontSize(32).Text("\xe1f7")
            );
}