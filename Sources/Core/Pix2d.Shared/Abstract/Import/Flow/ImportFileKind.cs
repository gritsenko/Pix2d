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

    /// <summary>Raster still images (.png / .jpg / .jpeg).</summary>
    Raster,

    /// <summary>Nothing we can import.</summary>
    Unsupported
}
