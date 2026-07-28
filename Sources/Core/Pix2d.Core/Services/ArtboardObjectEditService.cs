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
/// (invoked from the General action bar). The frame is a preview — the user confirms (one undoable
/// <see cref="ResizeArtboardScaleOperation"/> / <see cref="ResizeArtboardOperation"/>) or cancels, and either
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

    private readonly ArtboardLabelsLayer _labelsLayer;

    private ArtboardObjectEditorNode? _editor;
    private Pix2dSprite? _sprite;
    private SKPoint _origPos;
    private SKSize _origSize;
    private ArtboardObjectEditMode _mode = ArtboardObjectEditMode.Resize;

    public bool IsActive => _editor != null;
    public ArtboardObjectEditMode Mode => _mode;

    public ArtboardObjectEditService(AppState appState, IMessenger messenger, IOperationService operationService,
        IViewPortRefreshService viewPortRefreshService, IEditService editService, IDrawingService drawingService,
        IDialogService dialogService)
    {
        _appState = appState;
        _messenger = messenger;
        _operationService = operationService;
        _viewPortRefreshService = viewPortRefreshService;
        _editService = editService;
        _drawingService = drawingService;
        _dialogService = dialogService;

        _labelsLayer = new ArtboardLabelsLayer(
            () => _appState.CurrentProject.SceneNode?.Nodes.OfType<Pix2dSprite>() ?? Enumerable.Empty<Pix2dSprite>(),
            _editService.ActivateArtboard,
            _editService.EditArtboardAsObject);

        messenger.Register<ProjectLoadedMessage>(this, m => AttachLabels(m.ActiveScene));
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

        var editor = new ArtboardObjectEditorNode
        {
            OnChanged = () => _viewPortRefreshService.Refresh(),
        };
        editor.SetTarget(sprite, mode);
        _editor = editor;

        SkiaNodes.AdornerLayer.GetAdornerLayer(scene).Add(editor);
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
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

        Exit();
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

        _editor.RemoveFromParent();
        _editor = null;
        _sprite = null;

        // Re-point the shared drawing layer to the (possibly resized/cropped) active sprite so drawing
        // stays aligned, and bring the object selection frame back.
        _drawingService.UpdateDrawingTarget();
        _editService.ShowNodeEditor();
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    private void RaiseStateChanged() =>
        _messenger.Send(new ArtboardObjectEditStateChangedMessage(IsActive, _mode, _sprite));
}
