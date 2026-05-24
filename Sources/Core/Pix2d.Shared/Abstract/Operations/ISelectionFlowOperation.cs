namespace Pix2d.Abstract.Operations;

/// <summary>
/// Marker for operations that belong to the pixel-selection lifecycle (create marquee, transform, commit
/// transform). Consumers that care about "the user is still working with a marquee" — selection-aware
/// tools, dirty-state trackers — match on this interface instead of enumerating concrete operation types,
/// so new selection-flow operations and new selection-aware tools compose without touching every caller.
///
/// Note: implementing this interface does NOT exempt the operation from project-dirty tracking. Operations
/// that additionally mutate persisted pixel data (e.g. the commit step) implement
/// <see cref="ISpriteEditorOperation"/> as well and remain visible to autosave.
/// </summary>
public interface ISelectionFlowOperation : IEditOperation
{
}
