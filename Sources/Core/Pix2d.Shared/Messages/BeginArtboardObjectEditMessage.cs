using Pix2d.CommonNodes;

namespace Pix2d.Messages;

/// <summary>
/// Sent when the user double-clicks an artboard's name label (see ArtboardLabelsLayer), or from any other
/// entry point that wants to edit a sprite as a scene object. ArtboardObjectEditService handles it by
/// entering "edit sprite as object" mode (move + crop-resize frame) for that sprite.
/// Mirrors <see cref="ActivateArtboardRequestedMessage"/>.
/// </summary>
public class BeginArtboardObjectEditMessage(Pix2dSprite sprite)
{
    public Pix2dSprite Sprite { get; } = sprite;
}
