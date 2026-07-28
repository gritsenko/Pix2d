#nullable enable
using System.Threading.Tasks;
using Pix2d.CommonNodes;

namespace Pix2d.Abstract.Services;

/// <summary>
/// The Resize / Crop sub-modes of the General (objects) context, plus artboard renaming.
/// Selection and moving are NOT here — those live in the General context itself
/// (<c>ObjectManipulationTool</c> + the object selection frame); this service owns only the two
/// operations that need a dedicated handle-driven frame because they change the artboard's canvas.
/// </summary>
public interface IArtboardObjectEditService
{
    /// <summary>True while a Resize or Crop session is open.</summary>
    bool IsActive { get; }

    /// <summary>The sub-mode of the open session; meaningless when <see cref="IsActive"/> is false.</summary>
    ArtboardObjectEditMode Mode { get; }

    /// <summary>
    /// Opens a handle-driven frame over <paramref name="sprite"/> for a single Resize or Crop. The frame is
    /// a preview only — nothing is applied until <see cref="ConfirmMode"/>. No-op if a session is already open.
    /// </summary>
    void Begin(Pix2dSprite sprite, ArtboardObjectEditMode mode);

    /// <summary>Applies the framed change as one undoable operation and ends the session.</summary>
    void ConfirmMode();

    /// <summary>Discards the preview (nothing was applied) and ends the session.</summary>
    void CancelMode();

    /// <summary>Renames an artboard through an input dialog. Needs no session.</summary>
    Task RenameAsync(Pix2dSprite sprite);
}
