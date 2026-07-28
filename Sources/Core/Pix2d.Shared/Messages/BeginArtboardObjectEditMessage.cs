using Pix2d.Abstract;
using Pix2d.CommonNodes;

namespace Pix2d.Messages;

/// <summary>
/// Requests an artboard canvas-edit session (Resize / Crop) for <see cref="Sprite"/> — the decoupled
/// equivalent of calling <c>IArtboardObjectEditService.Begin</c>. Handled by ArtboardObjectEditService.
/// Mirrors <see cref="ActivateArtboardRequestedMessage"/>.
/// </summary>
public class BeginArtboardObjectEditMessage(Pix2dSprite sprite, ArtboardObjectEditMode mode)
{
    public Pix2dSprite Sprite { get; } = sprite;
    public ArtboardObjectEditMode Mode { get; } = mode;
}
