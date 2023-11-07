using Pix2d.Abstract.Commands;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d;

public class SpriteAnimationCommands : CommandsListBase, ISpriteAnimationCommands
{
    protected override string BaseName => "Sprite.Animation";
    private static SpriteEditor SpriteEditor => CoreServices.EditService.GetCurrentEditor() as SpriteEditor;

    public Pix2dCommand AddFrame =>
        GetCommand("Add frame", null, EditContextType.Sprite, () => { SpriteEditor.AddFrame(); }, behaviour: DisableOnAnimation.Instance);

    public Pix2dCommand DuplicateFrame =>
        GetCommand("Duplicate frame", null, EditContextType.Sprite, () => { SpriteEditor.DuplicateFrame(); }, behaviour: DisableOnAnimation.Instance);

    public Pix2dCommand TogglePlay => GetCommand("Play/Pause animation", null, EditContextType.Sprite,
        () => { SpriteEditor?.TogglePlay(); });

    public Pix2dCommand PrevFrame => GetCommand("Go to previous frame", new CommandShortcut(VirtualKeys.OEMComma, KeyModifier.Shift | KeyModifier.Ctrl), EditContextType.Sprite,
        () => { SpriteEditor?.PrevFrame(); }, behaviour: DisableOnAnimation.Instance);
    public Pix2dCommand NextFrame => GetCommand("Go to next frame", new CommandShortcut(VirtualKeys.OEMPeriod, KeyModifier.Shift | KeyModifier.Ctrl), EditContextType.Sprite,
        () => { SpriteEditor?.NextFrame(); }, behaviour: DisableOnAnimation.Instance);

    public Pix2dCommand DeleteFrame => GetCommand("Delete selected frame", null, EditContextType.Sprite,
        () => { SpriteEditor?.DeleteFrame(); }, behaviour: DisableOnAnimation.Instance);

    public Pix2dCommand Stop => GetCommand("Stop", null, EditContextType.Sprite,
        () => { SpriteEditor?.Stop(); });

}