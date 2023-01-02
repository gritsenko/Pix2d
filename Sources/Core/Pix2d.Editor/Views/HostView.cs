namespace Pix2d.Views;

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
            if (VisualRoot != null)
                VisualRoot.Renderer.DrawFps = false;
        };
#endif
    }
}