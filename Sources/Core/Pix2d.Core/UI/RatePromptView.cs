using Pix2d.Command;
using Pix2d.Common.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

// IReviewService is only registered on heads that support store review (e.g. Android); the default
// null lets ActivatorUtilities build this view on desktop/web too (where ShowRatePrompt simply stays
// false and the banner never appears) instead of throwing "unable to resolve IReviewService".
public partial class RatePromptView(IReviewService? reviewService = null, ICommandService commandService = null!)
    : ViewBase<RatePromptView.State>(new State(reviewService, commandService))
{
    // Content row: message + action buttons. Horizontal by default; the MainView "notify-content"
    // Narrow style flips it to vertical so the buttons wrap onto their own line on a phone-portrait
    // screen instead of the row overflowing off-screen.
    protected override object Build(State state) =>
        // NOTE: Orientation is driven by the MainView "notify-content" style (Horizontal wide /
        // Vertical narrow) — do NOT set it locally here, a local value would beat the Narrow style.
        new StackPanel()
            .Classes("notify-content")
            .Spacing(12)
            .Children(
                new TextBlock()
                    .Classes("body14")
                    .MaxWidth(240)
                    .Text(state, x => x.RatePromptMessage)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap),
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Command(state.WindowCommands.RateAppCommand)
                            .Content(state, x => x.RatePromptButtonText)
                            .Background(StaticResources.Brushes.AccentBrush)
                            // Accent fill needs crisp, fully-opaque white text — the theme default reads as dull grey.
                            .Foreground(Avalonia.Media.Brushes.White),
                        new Button()
                            .Classes("btn")
                            .Command(state.WindowCommands.CloseRatePromptCommand)
                            .Content(L("Not now"))
                    )
            );

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial string RatePromptMessage { get; set; } = "Rate please";

        [ObservableProperty]
        public partial string RatePromptButtonText { get; set; } = "Yes";

        public State(IReviewService? reviewService, ICommandService commandService)
        {
            WindowCommands = commandService.GetCommandList<WindowCommands>()!;
            RatePromptMessage = reviewService?.GetPromptMessage() ?? "Rate please";
            RatePromptButtonText = reviewService?.GetPromptButtonText() ?? "Yes";
        }

        public WindowCommands WindowCommands { get; }
    }
}