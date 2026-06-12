using Pix2d.Abstract;
using Pix2d.CommonNodes;

namespace Pix2d.Messages;

/// <summary>
/// Raised by ArtboardObjectEditService whenever the "edit sprite as object" session starts, ends, or
/// switches sub-mode. SpriteActionsView listens to this to show/hide itself and swap its button set
/// (Move: Resize / Crop / Set name / Done — Resize&amp;Crop: Apply / Cancel).
/// </summary>
public class ArtboardObjectEditStateChangedMessage(bool isActive, ArtboardObjectEditMode mode, Pix2dSprite? sprite)
{
    public bool IsActive { get; } = isActive;
    public ArtboardObjectEditMode Mode { get; } = mode;
    public Pix2dSprite? Sprite { get; } = sprite;
}
