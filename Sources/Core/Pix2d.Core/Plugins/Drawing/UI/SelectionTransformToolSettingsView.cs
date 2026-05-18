using Avalonia.Controls.Shapes;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.UI.Resources;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.Plugins.Drawing.UI;

/// <summary>
/// Settings panel for <see cref="PixelTransformTool"/>. Surfaces only transform-specific actions while the
/// selection is lifted: Apply / Flip / Rotate. Clipboard actions intentionally stay on the marquee toolbar and
/// disappear in transform mode so the top UI clearly communicates that we are editing the selected pixels now.
/// </summary>
public class SelectionTransformToolSettingsView(
    ICommandService commandService) : ViewBase
{
    private ISpriteEditCommands SpriteEditCommands =>
        commandService.GetCommandList<ISpriteEditCommands>() ??
        throw new InvalidOperationException("CommandService is not available");

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                // Apply — leave transform mode via the shared ApplySelection command so Enter / toolbar click
                // / click-outside all commit through the same return-to-selection-tool path.
                new Button()
                    .Command(SpriteEditCommands.ApplySelection)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.CheckIcon)),

                new Button()
                    .Command(SpriteEditCommands.FlipHorizontal)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.MirrorHorizontallyIcon)),
                new Button()
                    .Command(SpriteEditCommands.FlipVertical)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.MirrorVerticallyIcon)),
                new Button()
                    .Command(SpriteEditCommands.RotateMinus90)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.RotateContentLeftIcon)),
                new Button()
                    .Command(SpriteEditCommands.Rotate90)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.RotateContentRightIcon)),
                new Button()
                    .Command(SpriteEditCommands.Cancel)
                    .With(ButtonStyle)
                    .Content(CreateIcon(StaticResources.Icons.CursorRemoveSelectionIcon))

            );

    private static void ButtonStyle(Button b)
    {
        b.Classes("btn")
            .Width(48)
            .Height(48)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
            b.ToolTip_Tip(pc.Tooltip);
    }

    private static Path CreateIcon(Geometry geometry) =>
        new Path()
            .Data(geometry)
            .Stretch(Stretch.Uniform)
            .Width(18)
            .Height(18)
            // Source SVGs were exported with a negative Y matrix; flip them back here so the
            // resource paths can stay verbatim while the toolbar renders them upright.
            .RenderTransform(new ScaleTransform(1, -1))
            .RenderTransformOrigin(new RelativePoint(0.5, 0.5, RelativeUnit.Relative))
            .Fill(StaticResources.Brushes.ForegroundBrush);
}
