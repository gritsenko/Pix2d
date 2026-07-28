namespace Pix2d.Abstract;

/// <summary>
/// Sub-mode of an artboard canvas-edit session (see IArtboardObjectEditService). Both drag the same frame
/// handles and differ only in what is committed:
/// <list type="bullet">
/// <item><see cref="Resize"/> — scale the sprite content to the new frame size.</item>
/// <item><see cref="Crop"/> — change the canvas without scaling (trim / extend), keeping pixel scale.</item>
/// </list>
/// Moving an artboard is not a sub-mode — it is a plain drag in the General context.
/// </summary>
public enum ArtboardObjectEditMode
{
    Resize,
    Crop
}
