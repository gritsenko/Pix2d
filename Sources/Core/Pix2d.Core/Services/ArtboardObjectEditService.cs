#nullable enable
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using Pix2d.Messages.ViewPort;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services;

/// <summary>
/// Owns the artboard scene overlays and the "edit sprite as object" mode:
/// <list type="bullet">
/// <item>Keeps the always-on <see cref="ArtboardLabelsLayer"/> attached to the current scene's adorner layer.</item>
/// <item>On a label double-click (or <see cref="BeginArtboardObjectEditMessage"/>) enters object-edit mode in
/// <see cref="ArtboardObjectEditMode.Move"/>: the artboard is selected, dragged only by its name label, and a
/// <c>SpriteActionsView</c> toolbar offers Resize / Crop / Set name / Done.</item>
/// <item>Resize and Crop are explicit sub-modes with handles; the user confirms (one undoable
/// <see cref="ResizeArtboardScaleOperation"/> / <see cref="ResizeArtboardOperation"/>) or cancels from the
/// toolbar. A label drag commits one <see cref="MoveOperation"/> per gesture. A press outside the artboard in
/// Move mode (or the Done button) ends the session; Esc cancels the current sub-mode or exits.</item>
/// </list>
/// The view layer is driven by <see cref="ArtboardObjectEditStateChangedMessage"/>.
/// </summary>
public class ArtboardObjectEditService
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
    private ArtboardObjectEditMode _mode = ArtboardObjectEditMode.Move;

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
            Begin);

        messenger.Register<ProjectLoadedMessage>(this, m => AttachLabels(m.ActiveScene));
        messenger.Register<ViewPortInitializedMessage>(this, _ => AttachLabels(_appState.CurrentProject.SceneNode));
        messenger.Register<BeginArtboardObjectEditMessage>(this, m => Begin(m.Sprite));
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

    public void Begin(Pix2dSprite sprite)
    {
        // Already editing — ignore. (The backdrop exits Move mode before another label is reachable.)
        if (_editor != null)
            return;

        var scene = _appState.CurrentProject.SceneNode;
        if (scene == null)
            return;

        // Make it the active edit target so Layers/Timeline follow and the highlight border tracks it.
        _editService.ActivateArtboard(sprite);

        _sprite = sprite;
        _origPos = sprite.Position;
        _origSize = sprite.Size;
        _mode = ArtboardObjectEditMode.Move;

        var editor = new ArtboardObjectEditorNode
        {
            OnChanged = () => _viewPortRefreshService.Refresh(),
            BackdropPressed = Exit,
            MoveCompleted = CommitMove,
        };
        editor.SetTarget(sprite);
        _editor = editor;

        SkiaNodes.AdornerLayer.GetAdornerLayer(scene).Add(editor);
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    public void EnterResizeMode() => SetMode(ArtboardObjectEditMode.Resize);
    public void EnterCropMode() => SetMode(ArtboardObjectEditMode.Crop);

    private void SetMode(ArtboardObjectEditMode mode)
    {
        if (_editor == null || _mode == mode)
            return;

        _mode = mode;
        _editor.SetMode(mode);
        RaiseStateChanged();
    }

    /// <summary>Commits one undoable move for a finished label-drag gesture and re-bases the origin.</summary>
    private void CommitMove()
    {
        if (_sprite == null)
            return;

        var final = _sprite.Position;
        if (final != _origPos)
        {
            _sprite.Position = _origPos; // rewind so the operation snapshots the pre-move state in its ctor
            var op = new MoveOperation(new SKNode[] { _sprite });
            _sprite.Position = final;
            op.SetFinalData();
            _operationService.PushOperations(op);

            _origPos = final;
            _drawingService.UpdateDrawingTarget();
        }

        _viewPortRefreshService.Refresh();
    }

    /// <summary>Applies the current Resize/Crop frame as a single undoable operation and returns to Move mode.</summary>
    public void ConfirmMode()
    {
        if (_editor == null || _sprite == null || _mode == ArtboardObjectEditMode.Move)
            return;

        var target = _editor.FrameRect;
        var sprite = _sprite;

        // Resize/Crop only previews the frame rect — the sprite is untouched until now. Defensive reset so the
        // operation snapshots a clean original state in its ctor.
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

            _origPos = sprite.Position;
            _origSize = sprite.Size;
            _drawingService.UpdateDrawingTarget();
        }

        _mode = ArtboardObjectEditMode.Move;
        _editor.SetMode(ArtboardObjectEditMode.Move);
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    /// <summary>Discards the current Resize/Crop preview (frame-only, nothing applied) and returns to Move mode.</summary>
    public void CancelMode()
    {
        if (_editor == null || _mode == ArtboardObjectEditMode.Move)
            return;

        _mode = ArtboardObjectEditMode.Move;
        _editor.SetMode(ArtboardObjectEditMode.Move); // re-syncs the frame to the sprite's current bounds
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    /// <summary>Renames the edited artboard via an input dialog (label updates live; not undoable in v1).</summary>
    public async Task RenameAsync()
    {
        if (_sprite == null)
            return;

        var current = _sprite.Name ?? "";
        var result = await _dialogService.ShowInputDialogAsync(
            Pix2d.UI.LocalizationHelper.L("Artboard name"), Pix2d.UI.LocalizationHelper.L("Set name"), current);

        if (!string.IsNullOrWhiteSpace(result) && result != current)
        {
            _sprite.Name = result.Trim();
            _viewPortRefreshService.Refresh();
        }
    }

    /// <summary>Esc: cancel the active Resize/Crop sub-mode, or exit the whole session from Move mode.</summary>
    public void OnEscape()
    {
        if (_mode != ArtboardObjectEditMode.Move)
            CancelMode();
        else
            Exit();
    }

    /// <summary>Ends the whole object-edit session. Moves are already committed per gesture, so there is
    /// nothing left to apply here. Triggered by the Done button or a press outside the artboard.</summary>
    public void Exit()
    {
        if (_editor == null)
            return;

        _editor.RemoveFromParent();
        _editor = null;
        _sprite = null;
        _mode = ArtboardObjectEditMode.Move;

        // Re-point the shared drawing layer to the (possibly moved/resized) active sprite so drawing stays aligned.
        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService.Refresh();
        RaiseStateChanged();
    }

    private void RaiseStateChanged() =>
        _messenger.Send(new ArtboardObjectEditStateChangedMessage(IsActive, _mode, _sprite));
}
