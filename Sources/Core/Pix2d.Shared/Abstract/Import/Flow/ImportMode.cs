namespace Pix2d.Abstract.Import.Flow;

/// <summary>
/// How a set of dropped / picked files should be brought into the editor.
/// </summary>
public enum ImportMode
{
    /// <summary>Add the images as new layers of the current sprite.</summary>
    Layers,

    /// <summary>Create one new sprite (artboard) per image on the current scene.</summary>
    NewSprites,

    /// <summary>Group numbered files by base name and create one animated sprite per group.</summary>
    AnimationFrames,

    /// <summary>Unpack a .pix2d project and insert its sprites into the current scene.</summary>
    ProjectIntoScene,

    /// <summary>Open a project file, replacing the current project (future: in a new tab).</summary>
    OpenAsProject,

    /// <summary>Create a new sprite with animation from a GIF.</summary>
    Gif,

    /// <summary>
    /// Create one new sprite per layered document (.piskel), taking its layers and frames as decoded.
    /// Shares its execution with <see cref="Gif"/> — both are "one file decodes to one whole sprite" — and
    /// exists as its own mode so the decision log and any future per-format options stay distinguishable.
    /// </summary>
    LayeredDocument
}
