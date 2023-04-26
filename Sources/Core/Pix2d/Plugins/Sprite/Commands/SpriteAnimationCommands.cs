using Pix2d.Abstract.Commands;
using Pix2d.Command;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;

namespace Pix2d
{
    public class SpriteAnimationCommands : CommandsListBase
    {
        protected override string BaseName => "Sprite.Animation";
        private static SpriteEditor SpriteEditor => CoreServices.EditService.GetCurrentEditor() as SpriteEditor;

        public Pix2dCommand AddFrame =>
            GetCommand("Add frame", null, EditContextType.Sprite, () => { SpriteEditor.AddFrame(); });

        public Pix2dCommand DuplicateFrame =>
            GetCommand("Duplicate frame", null, EditContextType.Sprite, () => { SpriteEditor.DuplicateFrame(); });

        public Pix2dCommand TogglePlay => GetCommand("Play/Pause animation", null, EditContextType.Sprite,
            () => { SpriteEditor?.TogglePlay(); });

        public Pix2dCommand PrevFrame => GetCommand("Go to previous frame", null, EditContextType.Sprite,
            () => { SpriteEditor?.PrevFrame(); });
        public Pix2dCommand NextFrame => GetCommand("Go to next frame", null, EditContextType.Sprite,
            () => { SpriteEditor?.NextFrame(); });

        public Pix2dCommand DeleteFrame => GetCommand("Delete selected frame", null, EditContextType.Sprite,
            () => { SpriteEditor?.DeleteFrame(); });

        public Pix2dCommand Stop => GetCommand("Stop", null, EditContextType.Sprite,
            () => { SpriteEditor?.Stop(); });

    }
}