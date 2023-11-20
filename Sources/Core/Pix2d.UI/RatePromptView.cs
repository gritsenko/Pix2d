using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Resources;
using Sentry.Protocol;

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
                    .FontSize(14)
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .Text(()=>RatePromptMessage)
                    .Margin(16, 4, 16, 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap),

                new Button()
                    .FontSize(16)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Command(Commands.Window.RateAppCommand)
                    .Content(Bind(RatePromptButtonText))
                    .Background("#FFDB7B06".ToColor().ToBrush()),
                
                new Button()
                    .FontSize(14)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(4))
                    .Command(Commands.Window.CloseRatePromptCommand)
                    .Content("Not now")
            );

    [Inject] private AppState? AppState { get; set; } = null!;
    [Inject] private IReviewService? ReviewService { get; set; } = null!;
    public string RatePromptMessage => ReviewService?.GetPromptMessage() ?? "Rate please";
    public string RatePromptButtonText => ReviewService?.GetPromptButtonText() ?? "Yes";
}