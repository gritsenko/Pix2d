using Pix2d.Plugins.Sprite;
using Pix2d.Shared;

namespace Pix2d.Views;

public class ActionsBarView : ComponentBase
{
    private double ButtonWidth = 58;
    private double ButtonHeight = 58;

    void IconStyle(PathIcon icon) => icon
        .Width(16)
        .Height(16);

    protected override object Build() =>
        new ScrollViewer()
            .Background(StaticResources.Brushes.ActionsBarBackground)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Margin(0, 32, 0, 0)
            .Height(ButtonHeight)
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        new AppButton()
                            .Command(SpritePlugin.EditCommands.Rotate90)
                            .Width(ButtonWidth)
                            .Height(ButtonHeight)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M 8 0 L 8 2 C 4.691406 2 2 4.691406 2 8 C 2 11.308594 4.691406 14 8 14 C 11.128906 14 13.730469 11.613281 14 8.542969 C 14.019531 8.363281 13.9375 8.183594 13.789063 8.082031 C 13.644531 7.976563 13.449219 7.957031 13.285156 8.035156 C 13.121094 8.113281 13.011719 8.277344 13 8.457031 C 12.78125 11.011719 10.621094 13 8 13 C 5.230469 13 3 10.769531 3 8 C 3 5.230469 5.230469 3 8 3 L 8 5 L 11 2.5 Z M 12.5 4 C 12.222656 4 12 4.222656 12 4.5 C 12 4.777344 12.222656 5 12.5 5 C 12.777344 5 13 4.777344 13 4.5 C 13 4.222656 12.777344 4 12.5 4 Z M 13.5 6 C 13.222656 6 13 6.222656 13 6.5 C 13 6.777344 13.222656 7 13.5 7 C 13.777344 7 14 6.777344 14 6.5 C 14 6.222656 13.777344 6 13.5 6 Z ")))
                            .Label("Rotate"),

                        new AppButton()
                            .Command(SpritePlugin.EditCommands.FlipHorizontal)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M 8 1 L 8 13 L 14 13 Z M 7 1.007813 L 1.007813 13 L 7 13 Z M 6 5.242188 L 6 12 L 2.625 12 Z ")))
                            .Label("Flip X"),

                        new AppButton()
                            .Command(SpritePlugin.EditCommands.FlipVertical)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M 14 1 L 2 7 L 14 7 Z M 2 8 L 14 14 L 14 8 Z M 6.234375 9 L 13 9 L 13 12.382813 Z ")))
                            .Label("Flip Y"),

                        // MIRROR X
                        new AppToggleButton()
                            .IsChecked(MirrorX, BindingMode.TwoWay, bindingSource: this)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M8,14V0H9V14Zm9-2H11V2h6V12h0Zm-5-1h4V3H12ZM0,12V2H6V12Z")))
                            .Label("Mirror X"),

                        // MIRROR Y
                        new AppToggleButton()
                            .IsChecked(MirrorY, BindingMode.TwoWay, bindingSource: this)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M-393.477 -548.726L-403.477 -548.726L-403.477 -542.726L-393.477 -542.726L-393.477 -548.726L-393.477 -548.726ZM-391.477 -540.726L-405.477 -540.726L-405.477 -539.726L-391.477 -539.726L-391.477 -540.726L-391.477 -540.726ZM-403.478 -537.726L-403.478 -531.726L-393.478 -531.726L-393.478 -537.726L-403.478 -537.726L-403.478 -537.726ZM-402.477 -532.726L-402.477 -536.726L-394.477 -536.726L-394.477 -532.726L-402.477 -532.726L-402.477 -532.726Z")))
                            .Label("Mirror Y"),

                        //Grid settings
                        new AppButton()
                            .Ref(out var gridButton)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(StaticResources.Icons.GridIcon)
                            )
                            .Label("Grid")
                            .ContextFlyout(
                                new Flyout()
                                    .Ref(out var flyout)
                                    .Placement(PlacementMode.Bottom)
                                    .Content(new GridSettingsView())
                            )
                            .OnClick(() => flyout.ShowAt(gridButton)),

                        //Lock axis
                        new AppToggleButton()
                            .IsChecked(LockAxis, BindingMode.TwoWay, bindingSource: this)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M19,20H1V3H0L1,1H1l.5-1L3,3H2V19H19V18l3,1.5L19,21Zm-5-3H4V8H5a4,4,0,0,1,8,0h1v9h0ZM7,13a2,2,0,1,0,2-2A2,2,0,0,0,7,13ZM6,8h6A3,3,0,0,0,6,8Z")))
                            .Label("Lock axis"),

                        //Import
                        new AppButton()
                            .Command(Commands.Edit.Import)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M 2.5 1 C 1.675781 1 1 1.675781 1 2.5 L 1 12.5 C 1 13.324219 1.675781 14 2.5 14 L 12.5 14 C 13.324219 14 14 13.324219 14 12.5 L 14 10 L 13 10 L 13 12.5 C 13 12.78125 12.78125 13 12.5 13 L 2.5 13 C 2.21875 13 2 12.78125 2 12.5 L 2 2.5 C 2 2.21875 2.21875 2 2.5 2 L 12.5 2 C 12.78125 2 13 2.21875 13 2.5 L 13 5 L 14 5 L 14 2.5 C 14 1.675781 13.324219 1 12.5 1 Z M 8.273438 4.023438 L 4.792969 7.5 L 8.273438 10.980469 L 8.976563 10.269531 L 6.707031 8 L 14 8 L 14 7 L 6.707031 7 L 8.976563 4.726563 Z ")))
                            .Label("Import"),

                        //Resize
                        new AppButton()
                            .Command(Commands.View.ToggleCanvasSizePanelCommand)
                            .Width(ButtonWidth)
                            .Content(new PathIcon()
                                .With(IconStyle)
                                .Data(Geometry.Parse(
                                    "M 3 1 L 3 3 L 1 3 L 1 4 L 3 4 L 3 12 L 11 12 L 11 14 L 12 14 L 12 12 L 14 12 L 14 11 L 4.707031 11 L 11 4.707031 L 11 10 L 12 10 L 12 3.707031 L 13.355469 2.351563 L 12.644531 1.648438 L 11.292969 3 L 5 3 L 5 4 L 10.292969 4 L 4 10.292969 L 4 1 Z ")))
                            .Label("Resize")

                    )
            );

    [Inject] private IDrawingService DrawingService { get; set; } = null!;
    [Inject] private ISnappingService SnappingService { get; set; } = null!;

    private bool _mirrorX;
    private bool _mirrorY;
    private bool _lockAxis;

    public bool MirrorX
    {
        get => _mirrorX;
        set
        {
            _mirrorX = value;
            DrawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Horizontal, value);
        }
    }

    public bool MirrorY
    {
        get => _mirrorY;
        set
        {
            _mirrorY = value;
            DrawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Vertical, value);
        }
    }

    public bool LockAxis
    {
        get => _lockAxis;
        set
        {
            _lockAxis = value;
            SnappingService.ForceAspectLock = value;
        }
    }

}