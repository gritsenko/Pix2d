using System;

namespace Pix2d.Views.MainMenu;

public class MainMenuItemView : ComponentBase
{
    public const string SelectedClass = "selected";
    protected override object Build() =>
        new Button()
            .FontSize(16)
            .Content(
                new Grid().Cols("32,*")
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Children(
                        new TextBlock().Col(0) //icon
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            //.IsVisible(!itemVm.IsSplitter)
                            .Text(Bind(Icon)),
                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(Bind(Header))
                    )
            )
            .Padding(8, 8, 8, 8)
            .HorizontalContentAlignment(HorizontalAlignment.Left)
            .OnClick(_ => { OnClicked(this); })
            .CommandParameter(this);

    private bool _isSelected;
    public event EventHandler<MainMenuItemView>? Clicked;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            
            if (value)
            {
                this.Classes.Add(SelectedClass);
            }
            else
            {
                this.Classes.Remove(SelectedClass);
            }
            _isSelected = value;
        }
    }

    public string Header { get; set; } = "null!";
    public string Icon { get; set; } = "";

    protected virtual void OnClicked(MainMenuItemView e)
    {
        Clicked?.Invoke(this, e);
    }
}