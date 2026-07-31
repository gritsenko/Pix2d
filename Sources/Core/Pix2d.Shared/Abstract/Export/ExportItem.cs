#nullable enable
using SkiaNodes;

namespace Pix2d.Abstract.Export;

/// <summary>
/// One unit of export output: the nodes to render plus the base file name (no extension) every file
/// produced for it must be derived from. One artboard = one item, so a batch export of N artboards is
/// N items and the exporter never has to guess how to name anything.
/// </summary>
/// <param name="Name">Base file name, already sanitized for the filesystem (see <see cref="ExportFileNames"/>).</param>
/// <param name="Nodes">Nodes rendered into this item's output.</param>
public sealed record ExportItem(string Name, IReadOnlyList<SKNode> Nodes);

/// <summary>
/// Which artboards an export covers. Surfaced in the Export dialog as a two-way switch; a project with
/// a single artboard hides it because both options resolve to the same thing.
/// </summary>
public enum ExportScope
{
    /// <summary>The artboards selected in the General (objects) context, falling back to the artboard
    /// currently being edited — which is the only meaningful "selection" in the Sprite context.</summary>
    SelectedSprites,

    /// <summary>Every artboard in the scene, in scene order.</summary>
    AllSprites
}
