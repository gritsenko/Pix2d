using Pix2d.CommonNodes;

namespace Pix2d.Messages;

/// <summary>
/// Sent by drawing tools when the user presses on a sprite (artboard) other than the active one.
/// EditService handles it by switching the active edit target to that sprite.
/// </summary>
public class ActivateArtboardRequestedMessage(Pix2dSprite sprite)
{
    public Pix2dSprite Sprite { get; } = sprite;
}
