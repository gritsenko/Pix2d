#nullable enable
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using Pix2d.Messages.ViewPort;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services;

/// <summary>
/// Owns the artboard scene overlays and the two canvas-edit sub-modes of the General (objects) context:
/// <list type="bullet">
/// <item>Keeps the always-on <see cref="ArtboardLabelsLayer"/> attached to the current scene's adorner layer.
/// A single click on a label activates that artboard; a double-click hands it to
/// <see cref="IEditService.EditArtboardAsObject"/>, i.e. enters the General context with it selected.</item>
/// <item><see cref="Begin"/> opens a handle-driven frame over one artboard for a single Resize or Crop
/// (invoked from the General action bar). The frame previews the result live — Resize replaces the artboard
/// on screen with a stretched snapshot (the sprite is render-suppressed for the session), Crop dims
/// everything outside the frame — but the document is untouched until the user confirms (one undoable
/// <see cref="ResizeArtboardScaleOperation"/> / <see cref="ResizeArtboardOperation"/>) or cancels; either
/// way the session ends and control returns to the General context.</item>
/// </list>
/// Selecting, moving, deleting and arranging artboards are NOT here — they are plain General-context
/// interactions (<c>ObjectManipulationTool</c> + the object selection frame + the object commands).
/// The view layer is driven by <see cref="ArtboardObjectEditStateChangedMessage"/>.
/// </summary>
public class ArtboardObjectEditService : IArtboardObjectEditService
{
    private readonly AppState _appState;
    private readonly IMessenger _messenger;
    private readonly IOperationService _operationService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IEditService _editService;
    private readonly IDrawingService _drawingService;
    private readonly IDialogService _dialogService;
    private readonly ISelectionService _selectionService;

    private readonly ArtboardLabelsLayer _labelsLayer;

    private ArtboardObjectEditorNode? _editor;
    private Pix2dSprite? _sprite;
    private SKPoint _origPos;
    private SKSize _origSize;
    private ArtboardObjectEditMode _mode = ArtboardObjectEditMode.Resize;
    private bool _keepAspect = true;

    public bool IsActive => _editor != null;
    public ArtboardObjectEditMode Mode => _mode;
    public SKRect FrameRect => _editor?.FrameRect ?? SKRect.Empty;
    public SKSize OriginalSize => _editor != null ? _origSize : SKSize.Empty;

    /// <inheritdoc />
    public bool KeepAspect
    {
        get => _keepAspect;
        set
        {
            if (_keepAspect == value)
                return;

            _keepAspect = value;

            // The overlay reads this on every move, so an open session picks the change up on the next drag
            // (and mid-drag, if the user toggles while holding a handle).
            if (_editor != null)
                _editor.KeepAspect = value;
        }
    }

    public ArtboardObjectEditService(AppState appState, IMessenger messenger, IOperationService operationService,
        IViewPortRefreshService viewPortRefreshService, IEditService editService, IDrawingService drawingService,
        IDialogService dialogService, ISelectionService selectionService)
    {
        _appState = appState;
        _messenger = messenger;
        _operationService = operationService;
        _viewPortRefreshService = viewPortRefreshService;
        _editService = editService;
        _drawingService = drawingService;
        _dialogService = dialogService;
        _selectionService = selectionService;

        _labelsLayer = new ArtboardLabelsLayer(
            () => _appState.CurrentProject.SceneNode?.Nodes.OfType<Pix2dSprite>() ?? Enumerable.Empty<Pix2dSprite>(),
            _editService.ActivateArtboard,
            _editService.EditArtboardAsObject,
            // Pinned against the layer's zoom-out declutter pass: a selected object keeps its name on screen.
            sprite => selectionService.Selection?.Nodes.Contains(sprite) == true,
            () => _viewPortRefreshService.Refresh());

        // A session belongs to one scene: it holds a sprite, an overlay on that scene's adorner layer and a
        // render-suppression flag on the target. Any scene swap under our feet ends it first, so none of the
        // three can leak into the new project (a leaked suppression flag = an artboard that never paints).
        messenger.Register<ProjectLoadedMessage>(this, m => { CancelMode(); AttachLabels(m.ActiveScene); });
        messenger.Register<ProjectActivatedMessage>(this, m => { CancelMode(); AttachLabels(m.Project.SceneNode); });
        messenger.Register<ViewPortInitializedMessage>(this, _ => AttachLabels(_appState.CurrentProject.SceneNode));
        messenger.Register<BeginArtboardObjectEditMessage>(this, m => Begin(m.Sprite, m.Mode));
    }

    /// <summary>Resolved eagerly at startup (see SpritePlugin) so message subscriptions are live before a project loads.</summary>
    public void Initialize() => AttachLabels(_appState.CurrentProject.SceneNode);

    private void AttachLabels(SKNode? scene)
    {
        if (scene == null)
            return;

        _labelsLayer.RemoveFromParent();
        SkiaNodes.AdornerLayer.GetAdornerLayer(scene).Add(_labelsLayer);
    }

    public void Begin(Pix2dSprite sprite, ArtboardObjectEditMode mode)
    {
        // Already editing — ignore. (The action bar only offers Apply/Cancel while a session is open.)
        if (_editor != null || sprite == null)
            return;

        var scene = _appState.CurrentProject.SceneNode;
        if (scene == null)
            return;

        // Make it the active edit target *and* the General-context selection, so Layers/Timeline follow it
        // and the session is entered from a consistent state however it was triggered (action bar, message).
        _editService.EditArtboardAsObject(sprite);

        // One frame on screen at a time: the General object-selection frame would otherwise sit under ours.
        _editService.HideNodeEditor();

        _sprite = sprite;
        _origPos = sprite.Position;
        _origSize = sprite.Size;
        _mode = mode;

        // Per-mode default rather than a sticky user preference: scaling artwork non-uniformly is the
        // exception (Resize starts locked), while cropping an arbitrary region is the point (Crop starts
        // unlocked). Shift inverts whatever is in force, so neither default blocks the other gesture.
        _keepAspect = mode == ArtboardObjectEditMode.Resize;

        var editor = new ArtboardObjectEditorNode
        {
            KeepAspect = _keepAspect,
            OnChanged = OnFrameChanged,
        };
        editor.SetTarget(sprite, mode);
        _editor = editor;

        // Resize draws a stretched stand-in for the artboard, so the real node must stop painting for the
        // duration — otherwise a shrinking frame leaves the original showing around the preview. Crop keeps
        // the sprite on screen (its shield dims what will be trimmed), and so does a Resize whose snapshot
        // failed. Runtime-only flag, never serialized (see SKNode.IsRenderSuppressed).
        sprite.IsRenderSuppressed = editor.PreviewsTargetContent;

        SkiaNodes.AdornerLayer.GetAdornerLayer(scene).Add(editor);
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    /// <inheritdoc />
    public void SetFrameSize(SKSize size) => _editor?.SetFrameSize(size);

    /// <summary>
    /// One live change of the working frame — a handle drag or a value typed into the action bar. Repaints
    /// the viewport and lets the bar's size / scale boxes follow the frame.
    /// </summary>
    private void OnFrameChanged()
    {
        _viewPortRefreshService.Refresh();
        _messenger.Send(new ArtboardObjectEditFrameChangedMessage(FrameRect));
    }

    /// <summary>Applies the framed Resize/Crop as a single undoable operation and ends the session.</summary>
    public void ConfirmMode()
    {
        if (_editor == null || _sprite == null)
            return;

        var target = _editor.FrameRect;
        var sprite = _sprite;

        // The frame is preview-only — the sprite is untouched until now. Defensive reset so the operation
        // snapshots a clean original state in its ctor.
        sprite.Position = _origPos;
        sprite.Size = _origSize;

        var newPos = new SKPoint(target.Left, target.Top);
        var sizeChanged = Math.Abs(target.Width - _origSize.Width) > 0.5f
                          || Math.Abs(target.Height - _origSize.Height) > 0.5f;
        var moved = newPos != _origPos;

        // Exit() unconditionally: it clears the render-suppression flag the Resize preview set, and an
        // artboard that stopped painting must not survive a failed operation.
        try
        {
            if (sizeChanged || moved)
            {
                if (_mode == ArtboardObjectEditMode.Crop)
                {
                    // Crop semantics: keep pixel scale, change the canvas; reposition so kept content stays anchored.
                    var localBounds = new SKRect(
                        target.Left - _origPos.X, target.Top - _origPos.Y,
                        target.Right - _origPos.X, target.Bottom - _origPos.Y);
                    _operationService.InvokeAndPushOperations(new ResizeArtboardOperation(sprite, localBounds, newPos));
                }
                else // Resize: scale the pixel content to the new size.
                {
                    _operationService.InvokeAndPushOperations(new ResizeArtboardScaleOperation(sprite, target.Size, newPos));
                }
            }
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>Discards the preview (frame-only, nothing applied) and ends the session.</summary>
    public void CancelMode() => Exit();

    /// <summary>Renames an artboard via an input dialog (label updates live; not undoable in v1).</summary>
    public async Task RenameAsync(Pix2dSprite sprite)
    {
        if (sprite == null)
            return;

        var current = sprite.Name ?? "";
        var result = await _dialogService.ShowInputDialogAsync(
            Pix2d.UI.LocalizationHelper.L("Artboard name"), Pix2d.UI.LocalizationHelper.L("Set name"), current);

        if (!string.IsNullOrWhiteSpace(result) && result != current)
        {
            sprite.Name = result.Trim();
            _viewPortRefreshService.Refresh();
        }
    }

    /// <summary>Ends the session and restores the General-context object frame.</summary>
    private void Exit()
    {
        if (_editor == null)
            return;

        if (_sprite != null)
            _sprite.IsRenderSuppressed = false; // the stand-in is going away — the artboard paints itself again

        _editor.RemoveFromParent();
        _editor.Dispose();                      // frees the Resize preview snapshot
        _editor = null;
        _sprite = null;

        // Re-point the shared drawing layer to the (possibly resized/cropped) active sprite so drawing
        // stays aligned, and bring the object selection frame back.
        _drawingService.UpdateDrawingTarget();
        ResyncObjectSelectionFrame();
        _editService.ShowNodeEditor();
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    /// <summary>
    /// Rebuilds the General-context selection frame from the artboard's *current* bounds. Needed because
    /// <c>NodesSelection.Invalidate</c> only recomputes bounds and keeps an existing frame node (the frame
    /// carries a rotation that bounds cannot restore), so after an applied Resize / Crop the object frame —
    /// and the size readout in its info badge — would still show the pre-edit canvas. Undo/redo already gets
    /// this treatment in <c>SelectionService</c>; an applied operation did not.
    /// </summary>
    private void ResyncObjectSelectionFrame()
    {
        var selection = _selectionService.Selection;
        if (selection == null)
            return;

        selection.ResetFrame();
        selection.Invalidate(); // → NodesSelectedMessage → EditService re-points the frame editor's thumbs
    }

    private void RaiseStateChanged() =>
        _messenger.Send(new ArtboardObjectEditStateChangedMessage(IsActive, _mode, _sprite));
}
