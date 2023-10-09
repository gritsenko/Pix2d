using Pix2d.Primitives;

namespace Pix2d.Abstract.Commands;

public interface ISpriteAnimationCommands : ICommandList
{
    Pix2dCommand AddFrame { get; }
    Pix2dCommand DuplicateFrame { get; }
    Pix2dCommand TogglePlay { get; }
    Pix2dCommand PrevFrame { get; }
    Pix2dCommand NextFrame { get; }
    Pix2dCommand DeleteFrame { get; }
    Pix2dCommand Stop { get; }
}