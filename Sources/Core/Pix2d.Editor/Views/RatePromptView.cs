using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public partial class RatePromptView : ViewBaseSingletonVm<MainViewModel>
{

    protected override object Build(MainViewModel vm) =>
        new StackPanel()
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Top)
            .IsVisible(Bind(vm.ShowRatePrompt))
            .Margin(new Thickness(0,4,0,0))
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.SelectedHighlighterBrush)
            .Children(
                new TextBlock()
                    .Text(Bind(vm.RatePromptMessage))
                    .Margin(16, 4, 16, 4)
                    .VerticalAlignment(VerticalAlignment.Center)
                    /*.TextWrapping(TextWrapping.WrapWholeWords)*/,

                new Button()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Command(ViewModel?.RateAppCommand)
                    .Content(Bind(vm.RatePromptButtonText))
                    .Background("#FFDB7B06".ToColor().ToBrush()),
                new Button()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(4))
                    .Command(ViewModel?.CloseRatePromptCommand)
                    .Content("Not now")
            );

}