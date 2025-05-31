using Avalonia;
using Pix2d.UI.Resources;
using Pix2d.Plugins.Sprite.Commands;

namespace Pix2d.Plugins.Drawing.UI;

public class ClipboardActionsView : ComponentBase
{
    [Inject] public ICommandService? CommandService { get; set; } = null!;
    private ISpriteEditCommands SpriteEditCommands => CommandService.GetCommandList<ISpriteEditCommands>();

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
                        //flyout.AddItem("Select object", SpriteEditCommands.SelectObjectCommand);
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
            b.ToolTip(pc.Tooltip);
        }
    }
}