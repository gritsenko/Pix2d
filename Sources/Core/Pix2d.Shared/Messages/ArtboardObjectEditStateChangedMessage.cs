using Pix2d.Abstract;
using Pix2d.CommonNodes;

namespace Pix2d.Messages;

/// <summary>
/// Raised by ArtboardObjectEditService whenever an artboard canvas-edit (Resize / Crop) session starts or
/// ends. <c>ArtboardCanvasEditView</c> listens to this to show its Apply / Cancel bar, and the General
/// action bar hides itself while a session is open.
/// </summary>
public class ArtboardObjectEditStateChangedMessage(bool isActive, ArtboardObjectEditMode mode, Pix2dSprite? sprite)
{
    public bool IsActive { get; } = isActive;
    public ArtboardObjectEditMode Mode { get; } = mode;
    public Pix2dSprite? Sprite { get; } = sprite;
}
