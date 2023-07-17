namespace Pix2d.Shared;

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
            .IsChecked(IsCheckedProperty)
            .Command(CommandProperty)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .Padding(0)
            .Margin(0)
            .Content(
                new Border()
                    .Background(BackgroundProperty)
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


    protected override void OnAfterInitialized()
    {
        
    }
}