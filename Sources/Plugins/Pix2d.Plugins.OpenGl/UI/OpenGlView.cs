using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Pix2d.Abstract.UI;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.OpenGl.UI;

public class OpenGlView : PopupView, IToolPanel
{
    protected override object Build()
    {
        Header = "3d view";
        return BuildPopup(() => //build popup itself
            new Grid().Width(1024)
                .Height(768)
                .Background(Brushes.Gray)
                .Children(
                    new OpenGlPageControl()
                        {
                            Yaw = 1
                        }
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
                            .OnClick(OnUpdateClocked)
                    )
                )
        );
    }

    private void OnUpdateClocked(RoutedEventArgs obj)
    {
        StateHasChanged();
    }

    private OpenGlPageControl _glContainer = null!;

    protected override void OnAfterInitialized()
    {
    }
}