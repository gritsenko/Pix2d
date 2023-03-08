using System;
using CommonServiceLocator;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.UI;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ViewCommands : CommandsListBase{
    protected override string BaseName => "View";

    public Pix2dCommand ZoomIn => GetCommand("Zoom In",
        new CommandShortcut(VirtualKeys.OEMPlus),
        EditContextType.General,
        () => CoreServices.ViewPortService.ViewPort.ZoomIn());

    public Pix2dCommand ZoomOut => GetCommand("Zoom Out",
        new CommandShortcut(VirtualKeys.OEMMinus),
        EditContextType.General,
        () => CoreServices.ViewPortService.ViewPort.ZoomOut());

    public Pix2dCommand ZoomAll => GetCommand("Zoom All",
        new CommandShortcut(VirtualKeys.N0),
        EditContextType.General,
        () =>
        {
            var vp = CoreServices.ViewPortService.ViewPort;
            if (Math.Abs(vp.Zoom - 1) < 0.01)
                CoreServices.ViewPortService.ShowAll();
            else
                vp.SetZoom(1);
        });

    public Pix2dCommand ToggleTimeline => GetCommand("Show/Hide timeline",
        new CommandShortcut(VirtualKeys.T, KeyModifier.Ctrl),
        EditContextType.General,
        () =>
        {
            var mc = ServiceLocator.Current.GetInstance<IMenuController>();
            mc.ShowTimeline = !mc.ShowTimeline;
        });


    public SnappingCommands Snapping { get; } = new SnappingCommands();

}