using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d;

public class SpriteAnimationCommands : CommandsListBase, ISpriteAnimationCommands
{
    protected override string BaseName => "Sprite.Animation";
    private SpriteEditor SpriteEditor => AppState.CurrentProject.CurrentNodeEditor as SpriteEditor;

    public Pix2dCommand AddFrame =>
        GetCommand(() => { SpriteEditor.AddFrame(); }, "Add frame", null, EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand DuplicateFrame =>
        GetCommand(() => { SpriteEditor.DuplicateFrame(); }, "Duplicate frame", null, EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand TogglePlay =>
        GetCommand(() => { SpriteEditor?.TogglePlay(); }, "Play/Pause animation", null, EditContextType.Sprite);

    public Pix2dCommand PrevFrame =>
        GetCommand(() => { SpriteEditor?.PrevFrame(); }, "Go to previous frame", new CommandShortcut(VirtualKeys.Left), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());
    public Pix2dCommand NextFrame =>
        GetCommand(() => { SpriteEditor?.NextFrame(); }, "Go to next frame", new CommandShortcut(VirtualKeys.Right), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand DeleteFrame =>
        GetCommand(() =>
        {
            if (!SpriteEditor.IsPlaying && (SpriteEditor?.FramesCount ?? 0) > 1)
            {
                SpriteEditor?.DeleteFrame();
            }
        }, "Delete selected frame", null, EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Stop =>
        GetCommand(() => { SpriteEditor?.Stop(); }, "Stop", null, EditContextType.Sprite);

}