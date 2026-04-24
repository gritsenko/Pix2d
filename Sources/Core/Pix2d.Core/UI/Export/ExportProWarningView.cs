using Pix2d.Command;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pix2d.UI.Export;

public partial class ExportProWarningView(ICommandService commandService) : ViewBase<ExportProWarningView.State>(new State(commandService))
{
    protected override object Build(State state) =>
        new Button().Background(Brushes.DeepSkyBlue)
            .Foreground(Brushes.White)
            .VerticalAlignment(VerticalAlignment.Top)
            // .IsHitTestVisible(false)
            .Command(state.ViewCommands.ShowLicensePurchaseCommand)
            .Content(
                new TextBlock()
                    .Text("Get PRO version now to disable Pix2d watermark")
                    .TextWrapping(TextWrapping.Wrap)
                    .FontSize(14)
            );

    public sealed partial class State : ObservableObject
    {
        public State(ICommandService commandService)
        {
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;
        }

        public ViewCommands ViewCommands { get; }
    }
}