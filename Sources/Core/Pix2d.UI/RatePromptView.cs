using Pix2d.Command;
using Pix2d.Common.Extensions;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public partial class RatePromptView : ComponentBase
{

    protected override object Build() =>
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
                    .Text(()=>RatePromptMessage)
                    .Margin(16, 4, 16, 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap),

                new Button()
                    .FontSize(16)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Command(WindowCommands.RateAppCommand)
                    .Content(Bind(RatePromptButtonText))
                    .Background("#FFDB7B06".ToColor().ToBrush()),
                
                new Button()
                    .FontSize(14)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(4))
                    .Command(WindowCommands.CloseRatePromptCommand)
                    .Content("Not now")
            );

    [Inject] private AppState? AppState { get; set; } = null!;
    [Inject] private IReviewService? ReviewService { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;
    private WindowCommands WindowCommands => CommandService.GetCommandList<WindowCommands>()!;
    public string? RatePromptMessage => ReviewService?.GetPromptMessage() ?? "Rate please";
    public string RatePromptButtonText => ReviewService?.GetPromptButtonText() ?? "Yes";
}