using System.Diagnostics.CodeAnalysis;
using Avalonia.Interactivity;

namespace Pix2d.UI;

//prevent from trimming [injected] services by native aot compilation
[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OpenGlView))]
public class OpenGlView() : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Background(Brushes.LightSkyBlue)
            .Children(
                new OpenGlPageControl()
                    .Ref(out _glContainer),

                new StackPanel().Children(
                    new TextBlock()
                        .Text("Info:"),
                    new TextBlock()
                        .MaxWidth(400)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(() => _glContainer.Info),
                    new Button()
                        .Content("Update")
                        .OnClick(OnUpdateClicked)
                )
            );

    private OpenGlPageControl _glContainer = null!;

    private void OnUpdateClicked(RoutedEventArgs obj)
    {
        _glContainer.Pitch += 0.1f;
        _glContainer.RequestNextFrameRendering();
        StateHasChanged();
    }

    protected override void OnAfterInitialized()
    {
    }

}