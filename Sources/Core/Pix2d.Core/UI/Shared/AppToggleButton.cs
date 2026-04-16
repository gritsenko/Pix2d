using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class AppToggleButton : AppButton
{
    public static readonly DirectProperty<AppToggleButton, bool> IsCheckedProperty
    = AvaloniaProperty.RegisterDirect<AppToggleButton, bool>(nameof(IsChecked), o => o.IsChecked, (o, v) => o.IsChecked = v);
    private bool _isChecked = false;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetAndRaise(IsCheckedProperty, ref _isChecked, value);
    }

    protected override object Build() =>
        new ToggleButton()
        .IsChecked(this, x => x.IsChecked, BindingMode.TwoWay, StaticResources.Converters.InverseBooleanConverter)
        .Command(this, x => x.Command, BindingMode.OneWay)
        .HorizontalAlignment(HorizontalAlignment.Stretch)
        .VerticalAlignment(VerticalAlignment.Stretch)
        .HorizontalContentAlignment(HorizontalAlignment.Stretch)
        .VerticalContentAlignment(VerticalAlignment.Stretch)
        .Content(
            new Border()
                .Background(this, x => x.Background, BindingMode.OneWay)
                .Child(
                    new Grid()
                        .Rows("24, Auto")
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Children(
                            new ContentControl()
                                .Name(IconControlName)
                                .FontSize(16)
                                .HorizontalAlignment(HorizontalAlignment.Center)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                .VerticalContentAlignment(VerticalAlignment.Stretch)
                                .FontFamily(this, x => x.IconFontFamily, BindingMode.OneWay)
                                .Content(this, x => x.Content, BindingMode.OneWay),

                            new TextBlock().Row(1)
                                .Name(LabelControlName)
                                .Text(this, x => x.Label, BindingMode.OneWay)
                                .HorizontalAlignment(HorizontalAlignment.Center)
                        )
                )
        );


    protected override void OnAfterInitialized()
    {

    }
}