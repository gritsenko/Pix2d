using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public partial class MainMenuItemView() : ViewBase<MainMenuItemView.State>(new State())
{
    public const string SelectedClass = "selected";

    protected override object Build(State state) =>
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
                            .Text(state, x => x.Icon),
                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                            .Text(state, x => x.Header)
                    )
            )
            .Padding(8, 8, 8, 8)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .HorizontalContentAlignment(HorizontalAlignment.Left)
            .OnClick(_ => Clicked?.Invoke(this, this));

    public event EventHandler<MainMenuItemView>? Clicked;

    public Type? ContentViewType { get; set; }

    public bool IsSelected
    {
        get => ViewModel?.IsSelected ?? false;
        set
        {
            if (IsSelected == value)
                return;

            ViewModel!.IsSelected = value;

            if (value)
            {
                this.Classes.Add(SelectedClass);
            }
            else
            {
                this.Classes.Remove(SelectedClass);
            }
        }
    }

    public string Header
    {
        get => ViewModel?.Header ?? string.Empty;
        set => ViewModel!.Header = value;
    }

    public string Icon
    {
        get => ViewModel?.Icon ?? string.Empty;
        set => ViewModel!.Icon = value;
    }

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [ObservableProperty]
        public partial string Header { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Icon { get; set; } = string.Empty;
    }
}