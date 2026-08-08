using Pix2d.Primitives;

namespace Pix2d.Abstract.Commands;

public interface ISpriteAnimationCommands : ICommandList
{
    Pix2dCommand AddFrame { get; }
    Pix2dCommand AddFrameAtEnd { get; }
    Pix2dCommand DuplicateFrame { get; }
    Pix2dCommand TogglePlay { get; }
    Pix2dCommand PrevFrame { get; }
    Pix2dCommand NextFrame { get; }
    Pix2dCommand DeleteFrame { get; }
    Pix2dCommand Stop { get; }

    /// <summary>Link every frame of the selected layer to the current frame's image (a linked cel).</summary>
    Pix2dCommand LinkAllFrames { get; }

    /// <summary>Give the current frame its own copy of a linked image again.</summary>
    Pix2dCommand UnlinkFrame { get; }
}