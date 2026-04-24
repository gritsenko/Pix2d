using CommunityToolkit.Mvvm.ComponentModel;

namespace Pix2d.UI.MainMenu;

public partial class AppSettingsView() : ViewBase<AppSettingsView.State>(new State())
{
    protected override object Build(State state) =>
        new TextBlock()
            .Text(L("App settings"));

    public sealed partial class State : ObservableObject
    {
    }
}