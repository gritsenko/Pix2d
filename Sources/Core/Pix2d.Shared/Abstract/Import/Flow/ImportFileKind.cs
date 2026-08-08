namespace Pix2d.Abstract.Import.Flow;

/// <summary>
/// Coarse classification of a dropped / picked file set, used to pick a default import mode.
/// </summary>
public enum ImportFileKind
{
    /// <summary>A project file (.pix2d / .pxm).</summary>
    Project,

    /// <summary>A single animated GIF.</summary>
    Gif,

    /// <summary>
    /// A layered document that decodes to layers *and* frames in one go (.piskel). Distinct from
    /// <see cref="Raster"/> because there is nothing to decide: one file is always one animated sprite,
    /// and distinct from <see cref="Project"/> because it does not open as a Pix2d project.
    /// </summary>
    LayeredDocument,

    /// <summary>Raster still images (.png / .jpg / .jpeg).</summary>
    Raster,

    /// <summary>Nothing we can import.</summary>
    Unsupported
}
