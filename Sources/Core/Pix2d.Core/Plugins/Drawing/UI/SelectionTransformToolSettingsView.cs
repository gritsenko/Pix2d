using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.UI.Resources;

namespace Pix2d.Plugins.Drawing.UI;

/// <summary>
/// Settings panel for <see cref="PixelTransformTool"/>. Surfaces Flip / Rotate / Apply actions that only
/// make sense while a pixel selection is being transformed. Standard clipboard actions (Copy / Cut / Paste /
/// Crop / Fill) come from the shared <see cref="ClipboardActionsView"/> appended at the end.
/// </summary>
public class SelectionTransformToolSettingsView(ICommandService commandService, IToolService toolService) : ViewBase
{
    private ISpriteEditCommands SpriteEditCommands =>
        commandService.GetCommandList<ISpriteEditCommands>() ??
        throw new InvalidOperationException("CommandService is not available");

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                // Apply — leave transform mode by handing the selection back to PixelSelectRectTool, which
                // triggers PixelTransformTool.Deactivate → commit. Same effect as pressing M.
                new Button()
                    .With(ButtonStyle)
                    .ToolTip_Tip("Apply transform")
                    .Content("\xE73E")
                    .OnClick(_ => toolService.ActivateTool<PixelSelectRectTool>()),

                new Button()
                    .Command(SpriteEditCommands.FlipHorizontal)
                    .With(ButtonStyle)
                    .Content("\xE13A"),
                new Button()
                    .Command(SpriteEditCommands.FlipVertical)
                    .With(ButtonStyle)
                    .Content("\xE174"),
                new Button()
                    .Command(SpriteEditCommands.Rotate90)
                    .With(ButtonStyle)
                    .Content("\xE7AD"),

                // Inline the standard clipboard actions so the user doesn't lose access while inside the
                // transform tool — Copy / Cut are still meaningful with a lifted selection.
                ViewFactory.Create<ClipboardActionsView>()
            );

    private static void ButtonStyle(Button b)
    {
        b.Classes("btn")
            .Width(48)
            .Height(48)
            .FontSize(16)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
            b.ToolTip_Tip(pc.Tooltip);
    }
}
