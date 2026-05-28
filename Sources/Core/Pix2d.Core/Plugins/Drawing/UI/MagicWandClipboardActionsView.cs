using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.Plugins.Drawing.UI;

public class MagicWandClipboardActionsView(ICommandService commandService) : ViewBase
{
    private ISpriteEditCommands SpriteEditCommands =>
        commandService.GetCommandList<ISpriteEditCommands>() ??
        throw new InvalidOperationException("CommandService is not available");

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                new Button()
                    .Command(SpriteEditCommands.TryPaste)
                    .With(ButtonStyle)
                    .Content("\xE77F"),
                new Button()
                    .Command(SpriteEditCommands.CopyPixels)
                    .With(ButtonStyle)
                    .Content("\xE8C8"),
                new Button()
                    .Command(SpriteEditCommands.CutPixels)
                    .With(ButtonStyle)
                    .Content("\xE8C6"),
                new AppButton()
                    .Width(48)
                    .Height(48)
                    .Label(string.Empty)
                    .Command(SpriteEditCommands.FillSelectionCommand)
                    .ToolTip_Tip(L(SpriteEditCommands.FillSelectionCommand.Tooltip))
                    .Content(CreateFillIcon())
            );

    private static Path CreateFillIcon() =>
        new Path()
            .Data(StaticResources.Icons.FillToolIcon)
            .Stretch(Stretch.Uniform)
            .Width(20)
            .Height(20)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .RenderTransform(new ScaleTransform(1, -1))
            .RenderTransformOrigin(new RelativePoint(0.5, 0.5, RelativeUnit.Relative))
            .Fill(StaticResources.Brushes.ForegroundBrush);

    private void ButtonStyle(Button b)
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