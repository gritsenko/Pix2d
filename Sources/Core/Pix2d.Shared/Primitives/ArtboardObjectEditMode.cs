namespace Pix2d.Abstract;

/// <summary>
/// Sub-mode of the "edit sprite as object" session (see ArtboardObjectEditService):
/// <list type="bullet">
/// <item><see cref="Move"/> — default after the artboard is selected: drag by its name label only, no handles.</item>
/// <item><see cref="Resize"/> — drag the frame handles to scale the sprite content to a new size.</item>
/// <item><see cref="Crop"/> — drag the frame handles to change the canvas without scaling (trim / extend).</item>
/// </list>
/// </summary>
public enum ArtboardObjectEditMode
{
    Move,
    Resize,
    Crop
}
