using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public partial class CommandsView(IPlatformStuffService platformStuffService) : ViewBase<CommandsView.State>(new State(platformStuffService))
{
    protected override object Build(State state) =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).Children(
                new TextBlock().Text(L("Keyboard shortcuts:")).Margin(0, 32, 0, 16).FontSize(20)
                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                ViewFactory.Create<KeyShortcutsView>()
                    .IsVisible(state.HasKeyboard)
            ));

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial bool HasKeyboard { get; set; }

        public State(IPlatformStuffService platformStuffService)
        {
            HasKeyboard = platformStuffService.HasKeyboard;
        }
    }
}
