using SkiaSharp;

namespace Pix2d.Messages;

/// <summary>
/// Raised by ArtboardObjectEditService on every live change of an open canvas-edit session's working frame —
/// a handle drag or a value typed into the action bar. Carries the world-space frame (what Apply would
/// commit), so <c>ArtboardCanvasEditView</c>'s size / scale boxes can follow a drag without polling.
/// Session start / end is a different signal: <see cref="ArtboardObjectEditStateChangedMessage"/>.
/// </summary>
public class ArtboardObjectEditFrameChangedMessage(SKRect frameRect)
{
    public SKRect FrameRect { get; } = frameRect;
}
