using Avalonia.LogicalTree;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Pix2d.UI;

public class HostView : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Children(
                new TextBlock()
                    .Ref(out _textBlock)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Foreground(Colors.White.ToBrush())
                    .Text("Loading..."),
                new ProgressBar()
                    .Ref(out _progressBar)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Width(100)
                    .IsIndeterminate(true)
                    .Margin(top: 50)
            );

    private TextBlock _textBlock;
    private ProgressBar _progressBar;
    public void LoadMainView(Type mainViewType, IServiceProvider serviceProvider)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                if (ActivatorUtilities.CreateInstance(serviceProvider, mainViewType) is not ViewBase mainLayoutView)
                    throw new Exception("Can't load main view!");

                mainLayoutView.ViewInitialized += () => UpdateCanvas(mainLayoutView, serviceProvider);
                
                Child = mainLayoutView;
                UpdateCanvas(mainLayoutView, serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                _textBlock.Text = ex.Message;
                _progressBar.IsVisible = false;
            }
        });
    }

    private static void UpdateCanvas(ViewBase mainView, IServiceProvider serviceProvider)
    {
        if (mainView.Child == null)
            throw new Exception("Main view.Child is null");

        var container = mainView.Child
            .GetLogicalChildren()
            .OfType<Border>()
            .FirstOrDefault(x => x.Name == "Pix2dCanvasContainer");

        if (container is Decorator dec)
            dec.Child = new SkiaCanvas(serviceProvider);
    }

}