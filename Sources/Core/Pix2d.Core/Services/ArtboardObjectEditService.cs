#nullable enable
using Mvvm.Messaging;
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
/// <item>On a label double-click (or <see cref="BeginArtboardObjectEditMessage"/>) enters object-edit mode:
/// shows an <see cref="ArtboardObjectEditorNode"/> for the sprite (move + crop-resize, no rotation).</item>
/// <item>Click outside the frame = apply (one undoable <see cref="ResizeArtboardOperation"/> or
/// <see cref="MoveOperation"/>); Esc = cancel (restore original position/size, no operation).</item>
/// </list>
/// Edits are provisional during the session and only committed on apply, so a single Ctrl+Z reverts a whole
/// place/resize gesture.
/// </summary>
public class ArtboardObjectEditService
{
    private readonly AppState _appState;
    private readonly IOperationService _operationService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IEditService _editService;
    private readonly IDrawingService _drawingService;

    private readonly ArtboardLabelsLayer _labelsLayer;

    private ArtboardObjectEditorNode? _editor;
    private Pix2dSprite? _sprite;
    private SKPoint _origPos;
    private SKSize _origSize;

    public bool IsActive => _editor != null;

    public ArtboardObjectEditService(AppState appState, IMessenger messenger, IOperationService operationService,
        IViewPortRefreshService viewPortRefreshService, IEditService editService, IDrawingService drawingService)
    {
        _appState = appState;
        _operationService = operationService;
        _viewPortRefreshService = viewPortRefreshService;
        _editService = editService;
        _drawingService = drawingService;

        _labelsLayer = new ArtboardLabelsLayer(
            () => _appState.CurrentProject.SceneNode?.Nodes.OfType<Pix2dSprite>() ?? Enumerable.Empty<Pix2dSprite>(),
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
        // Already editing — ignore. (The editor's backdrop applies & exits before a second label is reachable.)
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

        var editor = new ArtboardObjectEditorNode
        {
            OnChanged = () => _viewPortRefreshService.Refresh(),
            ApplyRequested = Apply,
        };
        editor.SetTarget(sprite);
        _editor = editor;

        SkiaNodes.AdornerLayer.GetAdornerLayer(scene).Add(editor);
        _viewPortRefreshService.Refresh();
    }

    private void Apply()
    {
        if (_editor == null || _sprite == null)
        {
            EndMode();
            return;
        }

        var target = _editor.FrameRect;
        var sprite = _sprite;

        // Revert the live preview first so the operation snapshots a clean original state in its ctor.
        sprite.Position = _origPos;
        sprite.Size = _origSize;

        var newPos = new SKPoint(target.Left, target.Top);
        var sizeChanged = Math.Abs(target.Width - _origSize.Width) > 0.5f
                          || Math.Abs(target.Height - _origSize.Height) > 0.5f;

        if (sizeChanged)
        {
            // Crop-tool semantics: keep pixel scale, change the canvas; reposition so kept content stays anchored.
            var localBounds = new SKRect(
                target.Left - _origPos.X, target.Top - _origPos.Y,
                target.Right - _origPos.X, target.Bottom - _origPos.Y);
            _operationService.InvokeAndPushOperations(new ResizeArtboardOperation(sprite, localBounds, newPos));
        }
        else if (newPos != _origPos)
        {
            var op = new MoveOperation(new SKNode[] { sprite });
            sprite.Position = newPos;
            op.SetFinalData();
            _operationService.PushOperations(op);
        }

        EndMode();
    }

    public void Cancel()
    {
        if (_editor == null)
            return;

        if (_sprite != null)
        {
            _sprite.Position = _origPos;
            _sprite.Size = _origSize;
        }

        EndMode();
    }

    private void EndMode()
    {
        _editor?.RemoveFromParent();
        _editor = null;
        _sprite = null;

        // Re-point the shared drawing layer to the (possibly moved/resized/restored) active sprite so drawing
        // stays aligned. Resize already triggers this via IUpdateDrawingTarget; this covers move and cancel.
        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService.Refresh();
    }
}
