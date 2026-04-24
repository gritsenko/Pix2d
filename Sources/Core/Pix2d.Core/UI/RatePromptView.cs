using Pix2d.Command;
using Pix2d.Common.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public partial class RatePromptView(IReviewService? reviewService, ICommandService commandService)
    : ViewBase<RatePromptView.State>(new State(reviewService, commandService))
{
    protected override object Build(State state) =>
        new StackPanel()
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Top)
            .Margin(new Thickness(0, 4, 0, 0))
            .Orientation(Orientation.Horizontal)
            .Background("#994384de".ToColor().ToBrush())
            .Children(
                new TextBlock()
                    .MaxWidth(220)
                    .FontSize(12)
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .Text(state, x => x.RatePromptMessage)
                    .Margin(16, 4, 16, 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap),
                new Button()
                    .FontSize(16)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Command(state.WindowCommands.RateAppCommand)
                    .Content(state, x => x.RatePromptButtonText)
                    .Background("#FFDB7B06".ToColor().ToBrush()),
                new Button()
                    .FontSize(14)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(4))
                    .Command(state.WindowCommands.CloseRatePromptCommand)
                    .Content("Not now")
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