using Pix2d.UI.Resources;

namespace Pix2d.Plugins.Drawing.UI;

public class ClipboardActionsView(ICommandService commandService) : ViewBase
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
                new Button()
                    .Command(SpriteEditCommands.CropPixels)
                    .With(ButtonStyle)
                    .Content("\xE7A8"),
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new MenuFlyout() { Placement = PlacementMode.Bottom };
                        flyout.AddItem("Fill selection", SpriteEditCommands.FillSelectionCommand);
                        b.Click += (s, e) => flyout.ShowAt(b);
                    })
                    .Content("\xE10C")
            );


    private void ButtonStyle(Button b)
    {
        b.Classes("btn")
        .Width(48)
        .Height(48)
        .FontSize(16)
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
        {
            b.ToolTip_Tip(pc.Tooltip);
        }
    }
}
