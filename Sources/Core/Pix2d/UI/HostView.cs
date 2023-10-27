using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.LogicalTree;
using CommonServiceLocator;

namespace Pix2d.UI;

public class HostView : ViewBase
{
    protected override object Build() =>
        new Grid()
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Foreground(Colors.White.ToBrush())
                    .Text("Loading..."),
                new ProgressBar()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Width(100)
                    .IsIndeterminate(true)
                    .Margin(0,50,0,0)
    #region ui tests
            //new MyLabel()
            //    .VerticalAlignment(VerticalAlignment.Center)
            //    .HorizontalAlignment(HorizontalAlignment.Center),

            //new TextBlock()
            //    .FontFamily(StaticResources.IconFontSegoe)
            //    .VerticalAlignment(VerticalAlignment.Center)
            //    .HorizontalAlignment(HorizontalAlignment.Center)
            //    .Foreground(Colors.White.ToBrush())
            //    .FontSize(22)
            //    .Margin(0,-50,0,0)
            //    .Text("\xE7A7"),

            //new Button()
            //    .FontFamily(StaticResources.IconFontSegoe)
            //    .VerticalAlignment(VerticalAlignment.Center)
            //    .HorizontalAlignment(HorizontalAlignment.Center)
            //    .Foreground(Colors.White.ToBrush())
            //    .FontSize(22)
            //    .Margin(-100, -100, 0, 0)
            //    .Content("\xE7A7")
    #endregion
            );

    public HostView() : base()
    {
#if DEBUG
        //this.AttachDevTools();
        AttachedToVisualTree += (o, args) =>
        {
            //DARW FPS was deprecated
            //if (VisualRoot != null)
            //    VisualRoot.Renderer.DrawFps = false;
        };
#endif
    }
    public async Task LoadMainView()
    {
        //hack: даём загрузиться сплеш скрину с полоской загрузки
        await Task.Delay(300);

        var mainViewType = ServiceLocator.Current.GetInstance<AppState>().Settings.MainViewType;
        var mainLayoutView = IoC.Get<SimpleContainer>().BuildInstance(mainViewType) as ViewBase;
        mainLayoutView.ViewInitialized += () => UpdateCanvas(mainLayoutView);

        Child = mainLayoutView;

        UpdateCanvas(mainLayoutView);
    }

    private void UpdateCanvas(ViewBase mainView)
    {
        var container = mainView.Child
            .GetLogicalChildren()
            .OfType<Border>()
            .FirstOrDefault(x => x.Name == "Pix2dCanvasContainer");
        container.Child = new SkiaCanvas();
    }

}