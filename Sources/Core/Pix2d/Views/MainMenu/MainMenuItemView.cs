using System;
using static Avalonia.Media.Brushes;

namespace Pix2d.Views.MainMenu;

public class MainMenuItemView : ComponentBase
{
    protected override object Build() =>
        new Button()
            .BindClass(IsSelected, "selected")
            .Background(@Background)
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

    private object? _tabContentInstance;
    private bool _isSelected;
    public event EventHandler<MainMenuItemView>? Clicked;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(Background));
        }
    }

    public Brush Background => IsSelected ? StaticResources.Brushes.ButtonHoverBrush : Colors.Transparent.ToBrush();

    public string Header { get; set; } = "null!";
    public string Icon { get; set; } = "";
    public Type? TabViewType { get; set; }

    protected virtual void OnClicked(MainMenuItemView e)
    {
        Clicked?.Invoke(this, e);
    }

    public ComponentBase? GetTabContent()
    {
        if (TabViewType == null)
            return null;

        _tabContentInstance ??= Activator.CreateInstance(TabViewType);
        return _tabContentInstance as ComponentBase;
    }
}