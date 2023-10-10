using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public partial class RatePromptView : ComponentBase
{

    protected override object Build() =>
        new StackPanel()
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Top)
            .IsVisible(Bind(ShowRatePrompt))
            .Margin(new Thickness(0,4,0,0))
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.SelectedHighlighterBrush)
            .Children(
                new TextBlock()
                    .Text(Bind(RatePromptMessage))
                    .Margin(16, 4, 16, 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    /*.TextWrapping(TextWrapping.WrapWholeWords)*/,

                new Button()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Command(Commands.Window.RateAppCommand)
                    .Content(Bind(RatePromptButtonText))
                    .Background("#FFDB7B06".ToColor().ToBrush()),
                new Button()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(4))
                    .Command(Commands.Window.CloseRatePromptCommand)
                    .Content("Not now")
            );

    public bool ShowRatePrompt { get; set; }
    public string RatePromptMessage { get; set;}
    public string RatePromptButtonText { get; set;}

    protected override void OnAfterInitialized()
    {
        throw new NotImplementedException("Надо доделать биндинги");
        base.OnAfterInitialized();
    }
}