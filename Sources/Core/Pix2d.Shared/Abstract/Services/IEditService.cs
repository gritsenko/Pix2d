#nullable enable
using System.Collections.Generic;
using Pix2d.Abstract.Import;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IEditService
{
    void ShowNodeEditor();

    void HideNodeEditor();

    void RequestEdit(SKNode[] nodes);

    /// <summary>
    /// Makes <paramref name="sprite"/> the active edit target (the sprite drawing/animation acts on).
    /// No-op if it is already active. Used to switch between several artboards on one scene.
    /// </summary>
    void ActivateArtboard(Pix2dSprite sprite);

    /// <summary>
    /// Pure query (no side effects): returns the artboard whose bounds contain <paramref name="worldPos"/>
    /// and which is NOT the currently edited one, or <c>null</c>. Always <c>null</c> for single-artboard
    /// scenes. Used by the touch-input layer to decide whether a one-finger press should defer to a
    /// "tap activates / drag pans" gesture instead of panning straight away. Distinct from the click-to-
    /// activate resolver, which sends an activation message as a side effect.
    /// </summary>
    Pix2dSprite? GetInactiveArtboardAt(SKPoint worldPos);

    /// <summary>
    /// Creates a new empty sprite (artboard) of the given size, places it next to the existing
    /// artboards on the current scene, makes it the active edit target and frames the view. Undoable.
    /// </summary>
    Pix2dSprite AddArtboard(SKSize size);

    /// <summary>
    /// Creates one new sprite (artboard) per import entry, building its layers/frames from the
    /// supplied <see cref="ImportData"/>, lays them out to the right of the existing artboards,
    /// activates the first one and frames the view. The whole batch is a single undo step.
    /// </summary>
    IReadOnlyList<Pix2dSprite> AddArtboardsFromImportData(IReadOnlyList<(string Name, ImportData Data)> imports);

    /// <summary>
    /// Reparents the sprites of an unpacked project scene into the current scene, preserving their
    /// relative layout and placing the group to the right of the existing artboards. Activates the
    /// first inserted sprite and frames the view. The whole insert is a single undo step.
    /// </summary>
    IReadOnlyList<Pix2dSprite> InsertSpritesFromScene(SKNode loadedScene);

    void ApplyCurrentEdit();

    void Resize(IContainerNode containerNode, SKSize size);
    void CropCurrentSprite(SKSize size, float horizontalAnchor, float verticalAnchor);
    void CropCurrentSprite(SKRect newBounds);
    void ResizeCurrentSprite(SKSize size);
}