#nullable enable
using System.Diagnostics;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services;

public class EditService : IEditService
{
    private readonly IViewPortService _viewPortService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly SpriteEditor? _spriteEditor;

    private readonly ISelectionService _selectionService;
    private readonly AppState _appState;
    private ProjectState ProjectState => _appState.CurrentProject;
    private readonly IMessenger _messenger;
    private readonly IOperationService _operationService;
    private readonly IDialogService _dialogService;


    private EditContextType CurrentEditContextType
    {
        get => ProjectState.CurrentContextType;
        set => ProjectState.CurrentContextType = value;
    }

    private INodeEditor? CurrentNodeEditor
    {
        get => ProjectState.CurrentNodeEditor;
        set => ProjectState.CurrentNodeEditor = value;
    }

    // Created lazily per project: each tab owns its FrameEditorNode so adorners never
    // leak across scenes when switching projects. This instance frames scene-object selections:
    //  - PassShiftPressThrough: Shift+click must reach ObjectManipulationTool to toggle selection
    //    membership (pixel-selection marquees use their own FrameEditorNode without the flag);
    //  - move-only (no resize/rotate handles): the generic thumbs commit a plain TransformOperation,
    //    which would change a Pix2dSprite's Size without touching its layer bitmaps, and the pixel
    //    pipeline has no notion of a rotated canvas. Resizing/cropping an artboard goes through
    //    ArtboardObjectEditService's dedicated ResizeArtboardScaleOperation / ResizeArtboardOperation.
    private SKNode FrameEditorNode =>
        _appState.CurrentProject.FrameEditorNode ??=
            new FrameEditorNode
            {
                ReparentMode = NodeReparentMode.Overflow,
                PassShiftPressThrough = true,
                AllowResize = false,
                AllowRotate = false
            };

    private SpriteEditor SpriteEditor => _spriteEditor ?? throw new InvalidOperationException("SpriteEditor is not initialized");


    public EditService(IViewPortRefreshService viewPortRefreshService,
        IViewPortService viewPortService,
        ISelectionService selectionService,
        AppState appState,
        IMessenger messenger,
        SpriteEditor spriteEditor,
        IOperationService operationService,
        IDialogService dialogService)
    {
        _viewPortService = viewPortService;
        _viewPortRefreshService = viewPortRefreshService;
        _selectionService = selectionService;
        _appState = appState;
        _messenger = messenger;
        _operationService = operationService;
        _dialogService = dialogService;

        _spriteEditor = spriteEditor;


        _messenger.Register<ProjectLoadedMessage>(this, OnProjectLoadedMessage);
        _messenger.Register<NodesSelectedMessage>(this, OnNodesSelected);
        _messenger.Register<ActivateArtboardRequestedMessage>(this, m => ActivateArtboard(m.Sprite));
        _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private void OnNodesSelected(NodesSelectedMessage obj)
    {
        UpdateEditors();
    }

    private void OnOperationInvoked(OperationInvokedMessage msg)
    {
        if (msg.OperationType != OperationEventType.Undo && msg.OperationType != OperationEventType.Redo)
            return;

        // Undo of AddArtboard (or redo of a delete) can detach the active artboard from the scene.
        ActivateSurvivingArtboard();
    }

    /// <summary>
    /// Keeps the edit target valid after the scene lost nodes: if the active artboard is no longer in the
    /// scene, switch to a surviving one so the editor / drawing target never dangle on a detached node.
    /// With no artboards left, clear the target instead — the same zero-sprite state the project-load path
    /// already tolerates (staying in General with an empty scene is coherent; a dangling target is not).
    /// </summary>
    private void ActivateSurvivingArtboard()
    {
        var scene = _appState.CurrentProject.SceneNode;
        if (scene == null)
            return;

        var current = _appState.CurrentProject.CurrentEditedNode;
        if (current is Pix2dSprite && scene.Nodes.Contains(current))
            return;

        var survivor = scene.Nodes.OfType<Pix2dSprite>().FirstOrDefault();
        if (survivor != null)
        {
            ActivateArtboard(survivor);
        }
        else
        {
            CurrentNodeEditor = null;
            _appState.CurrentProject.CurrentEditedNode = null;
        }
    }

    private void OnProjectLoadedMessage(ProjectLoadedMessage message)
    {
        var scene = message.ActiveScene;

        // A scene may now contain several sprites (artboards). Activate the first one as the default
        // edit target. OfType is robust to a non-sprite node being first in the collection.
        var sprite = scene.Nodes.OfType<Pix2dSprite>().FirstOrDefault();
        if (sprite != null)
        {
            RequestEdit([sprite]);
            _viewPortService.ShowAll();
        }
    }

    private void UpdateEditors()
    {
        try
        {
            if (CurrentEditContextType == EditContextType.Sprite)
                return;

            var selection = ProjectState.Selection;
            if (selection == null || _appState.ToolsState.CurrentTool?.ToolInstance is IDrawingTool)
            {
                FrameEditorNode.IsVisible = false;
                return;
            }

            FrameEditorNode.IsVisible = true;
            ((FrameEditorNode)FrameEditorNode).SetSelection(selection!);
            var sceneNode = ProjectState.SceneNode;
            if (sceneNode != null)
            {
                var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(sceneNode);
                if (adornerLayer != null)
                {
                    adornerLayer.Add(FrameEditorNode);
                }
            }
        }
        finally
        {
            _viewPortRefreshService.Refresh();
        }
    }

    public void ShowNodeEditor()
    {
        if (ProjectState.HasSelection)
            FrameEditorNode.IsVisible = true;
    }

    public void HideNodeEditor()
    {
        FrameEditorNode.IsVisible = false;
    }

    public void ActivateArtboard(Pix2dSprite sprite)
    {
        if (sprite == null || ReferenceEquals(sprite, _appState.CurrentProject.CurrentEditedNode))
            return;

        // RequestEdit cycles CurrentNodeEditor (null -> SpriteEditor), which also makes the Layers/Timeline
        // panels reload for the newly activated sprite, and re-points the drawing target via SetTargetNode.
        RequestEdit([sprite]);
        _viewPortRefreshService.Refresh();
    }

    public void EditArtboardAsObject(Pix2dSprite sprite)
    {
        if (sprite == null)
            return;

        // Keep it the active edit target (so Layers / Timeline / drawing target follow it), then hand the
        // interaction over to the object tools — ActivateArtboard always lands in the Sprite context, so
        // the context switch has to come after it.
        ActivateArtboard(sprite);
        CurrentEditContextType = EditContextType.General;
        _selectionService.Select(sprite);
        _viewPortRefreshService.Refresh();
    }

    public async Task DeleteSelectedObjectsAsync()
    {
        // Snapshot the selection: the array is the live selection and ClearSelection below replaces it.
        var nodes = (_selectionService.Selection?.Nodes ?? []).ToArray();
        if (nodes.Length == 0 || ProjectState.SceneNode == null)
            return;

        var confirmed = await _dialogService.ShowYesNoDialog(
            BuildDeleteConfirmationMessage(nodes), L("Delete objects"), L("Delete"));
        if (!confirmed)
            return;

        // Clear first so no adorner keeps framing a node that is about to leave the scene.
        _selectionService.ClearSelection();
        _operationService.InvokeAndPushOperations(new DeleteNodesOperation(nodes));

        ActivateSurvivingArtboard();
        _viewPortRefreshService.Refresh();
    }

    private static string BuildDeleteConfirmationMessage(IReadOnlyList<SKNode> nodes)
    {
        var firstName = string.IsNullOrWhiteSpace(nodes[0].Name) ? L("Untitled") : nodes[0].Name;

        return nodes.Count == 1
            ? string.Format(L("Delete \"{0}\"?"), firstName)
            : string.Format(L("Delete {0} objects, starting with \"{1}\"?"), nodes.Count, firstName);
    }

    public void ArrangeSelectedObjects()
    {
        var sprites = (_selectionService.Selection?.Nodes ?? []).OfType<Pix2dSprite>().ToArray();
        if (sprites.Length < 2)
            return;

        // Name-prefix families ("icon-goal-*", "icon-star-*") each get their own row block, so arranging a
        // real asset set keeps the families readable instead of packing them in blind reading order.
        var groups = ArtboardNameGrouping.Group(sprites);

        // Dense near-square grid rather than a fixed wrap width: a world-space constant would pack 4 and
        // 40 artboards very differently, while ceil(sqrt(n)) columns stays compact at any count.
        var columns = (int)Math.Ceiling(Math.Sqrt(sprites.Length));
        var block = sprites.GetBounds();

        var ordered = new List<SKNode>(sprites.Length);
        var targets = new List<SKPoint>(sprites.Length);

        var y = block.Top;
        foreach (var group in groups)
        {
            // A wider gutter between groups than between the rows inside one group is what makes the
            // grouping visible on the canvas — there is no other chrome saying "these belong together".
            if (ordered.Count > 0)
                y += ArtboardGroupGap;

            var index = 0;
            while (index < group.Length)
            {
                var rowCount = Math.Min(columns, group.Length - index);
                var x = block.Left;
                var rowHeight = 0f;

                for (var i = 0; i < rowCount; i++)
                {
                    var node = group[index + i];
                    var bounds = node.GetBoundingBox();
                    ordered.Add(node);
                    targets.Add(new SKPoint(x, y));
                    x += bounds.Width + ArtboardGap;
                    rowHeight = MathF.Max(rowHeight, bounds.Height);
                }

                index += rowCount;
                y += rowHeight;
                if (index < group.Length)
                    y += ArtboardGap;
            }
        }

        // Constructed before the mutation — TransformOperation snapshots the initial state in its ctor.
        var operation = new MoveOperation(ordered);

        for (var i = 0; i < ordered.Count; i++)
        {
            // Shift by the delta rather than assigning Position: bounding boxes are global while Position
            // is parent-local, and only the delta is the same in both spaces.
            var current = ordered[i].GetBoundingBox().Location;
            ordered[i].Position += targets[i] - current;
        }

        operation.SetFinalData();
        _operationService.PushOperations(operation);

        _selectionService.Selection?.Invalidate();
        _viewPortRefreshService.Refresh();
    }

    public Pix2dSprite? GetInactiveArtboardAt(SKPoint worldPos)
    {
        var project = _appState.CurrentProject;
        var scene = project?.SceneNode;
        if (scene == null)
            return null;

        var sprites = scene.Nodes.OfType<Pix2dSprite>().ToArray();
        if (sprites.Length <= 1)
            return null;

        var hit = sprites.FirstOrDefault(s => s.GetBoundingBox().Contains(worldPos));
        if (hit == null || ReferenceEquals(hit, project!.CurrentEditedNode))
            return null;

        return hit;
    }

    private const float ArtboardGap = 16f;

    // Gutter between the name-prefix groups of an Arrange pass — wide enough to read as a separator next
    // to the plain gap between neighbours in the same group.
    private const float ArtboardGroupGap = ArtboardGap * 3;

    public Pix2dSprite AddArtboard(SKSize size)
    {
        var scene = _appState.CurrentProject.SceneNode
            ?? throw new InvalidOperationException("No active scene to add an artboard to.");

        var siblings = scene.Nodes.OfType<Pix2dSprite>().ToArray();

        var sprite = Pix2dSprite.CreateEmpty(size);
        sprite.Name = $"Artboard {siblings.Length + 1}";

        // Lay the new artboard to the right of the existing ones, tops aligned, with a small gap.
        sprite.Position = ComputeNextArtboardOrigin(siblings);

        // Mutate first, then push the operation for undo/redo (mirrors SpriteEditor.AddEmptyLayer).
        scene.Nodes.Add(sprite);
        _operationService.PushOperations(new CreateNodesOperation(new SKNode[] { sprite }));

        ActivateArtboard(sprite);
        _viewPortService.ShowAll();

        return sprite;
    }

    /// <summary>
    /// Origin for the next artboard: to the right of all existing artboards, tops aligned, with a gap.
    /// Returns <see cref="SKPoint.Empty"/> when there are no existing artboards.
    /// </summary>
    private static SKPoint ComputeNextArtboardOrigin(IReadOnlyList<Pix2dSprite> siblings)
    {
        if (siblings.Count == 0)
            return SKPoint.Empty;

        var right = siblings.Max(s => s.GetBoundingBox().Right);
        var top = siblings.Min(s => s.GetBoundingBox().Top);
        return new SKPoint(right + ArtboardGap, top);
    }

    public IReadOnlyList<Pix2dSprite> AddArtboardsFromImportData(IReadOnlyList<(string Name, ImportData Data)> imports)
    {
        var scene = _appState.CurrentProject.SceneNode
            ?? throw new InvalidOperationException("No active scene to add artboards to.");

        if (imports.Count == 0)
            return [];

        var siblings = scene.Nodes.OfType<Pix2dSprite>().ToArray();
        var origin = ComputeNextArtboardOrigin(siblings);

        var created = new List<Pix2dSprite>();
        var x = origin.X;
        foreach (var (name, data) in imports)
        {
            var sprite = Pix2dSprite.CreateEmpty(new SKSize(data.Size.Width, data.Size.Height));
            SpriteImportApplier.Apply(sprite, data);

            if (!string.IsNullOrWhiteSpace(name))
                sprite.Name = name;

            sprite.Position = new SKPoint(x, origin.Y);
            scene.Nodes.Add(sprite);
            created.Add(sprite);

            // Tile subsequent sprites to the right of this one.
            x += sprite.GetBoundingBox().Width + ArtboardGap;
        }

        // One grouped operation so the whole batch is a single undo step (mirrors AddArtboard).
        _operationService.PushOperations(new CreateNodesOperation(created));

        ActivateArtboard(created[0]);
        _viewPortService.ShowAll();

        return created;
    }

    public IReadOnlyList<Pix2dSprite> InsertSpritesFromScene(SKNode loadedScene)
    {
        var scene = _appState.CurrentProject.SceneNode
            ?? throw new InvalidOperationException("No active scene to insert sprites into.");

        var sprites = loadedScene.Nodes.OfType<Pix2dSprite>().ToArray();
        if (sprites.Length == 0)
            return [];

        var siblings = scene.Nodes.OfType<Pix2dSprite>().ToArray();
        var origin = ComputeNextArtboardOrigin(siblings);

        // Translate the whole imported group by one delta so its left/top edge starts at the computed
        // origin, preserving the relative layout the sprites had in the source project.
        var importedLeft = sprites.Min(s => s.GetBoundingBox().Left);
        var importedTop = sprites.Min(s => s.GetBoundingBox().Top);
        var dx = origin.X - importedLeft;
        var dy = origin.Y - importedTop;

        foreach (var sprite in sprites)
        {
            sprite.RemoveFromParent();
            sprite.Position = new SKPoint(sprite.Position.X + dx, sprite.Position.Y + dy);
            scene.Nodes.Add(sprite);
        }

        _operationService.PushOperations(new CreateNodesOperation(sprites));

        ActivateArtboard(sprites[0]);
        _viewPortService.ShowAll();

        return sprites;
    }

    public void RequestEdit(SKNode[] nodes)
    {
        Debug.WriteLine("Requested edit for selection");

        if (nodes.Length != 1)
            return;

        var node = nodes[0];
        CurrentNodeEditor = null;

        if (node is GroupNode group)
        {
            _selectionService.SetActiveGroup(group);
            CurrentEditContextType = EditContextType.General;
        }

        if (node is Pix2dSprite sprite)
        {
            sprite.InvalidateFrames();
            _appState.CurrentProject.CurrentEditedNode = node;
            SpriteEditor.SetTargetNode(node);
            CurrentNodeEditor = SpriteEditor;
            CurrentEditContextType = EditContextType.Sprite;
        }

        if (node is TextNode)
        {
            CurrentEditContextType = EditContextType.Text;
        }
    }

    public void GroupNodes(SKNode[] nodes)
    {
        var parent = nodes[0].Parent;
        if (parent == null) return;

        var newGroup = new GroupNode();
        newGroup.Name = "Group";
        foreach (var node in nodes)
        {
            newGroup.Nodes.Add(node);
        }

        parent.Nodes.Add(newGroup);
        newGroup.UpdateBoundsToContent();

        _selectionService.Select(newGroup);
    }

    public void UngroupNodes(GroupNode group)
    {
        var parent = group.Parent;
        if (parent == null) return;

        foreach (var node in group.Nodes.ToArray())
        {
            group.Nodes.Remove(node);
            parent.Nodes.Insert(group.Index, node);
        }

        group.RemoveFromParent();
    }

    public void Resize(IContainerNode containerNode, SKSize size)
    {
        containerNode.Size = size;
        UpdateEditors();
    }

    public void CropCurrentSprite(SKSize newSize, float horizontalAnchor, float verticalAnchor)
    {
        if (!(GetCurrentEditor() is SpriteEditor editor)) return;

        editor.Crop(newSize, horizontalAnchor, verticalAnchor);
        UpdateEditors();

        _messenger.Send(new CanvasSizeChangedMessage());
    }

    public void CropCurrentSprite(SKRect newBounds)
    {
        if (!(GetCurrentEditor() is SpriteEditor editor)) return;

        editor.Crop(newBounds);
        UpdateEditors();
    }

    public void ResizeCurrentSprite(SKSize newSize)
    {
        if (!(GetCurrentEditor() is SpriteEditor editor)) return;

        editor.Resize((int)newSize.Width, (int)newSize.Height);
        UpdateEditors();

        _messenger.Send(new CanvasSizeChangedMessage());
    }

    public INodeEditor GetCurrentEditor()
    {
        return CurrentNodeEditor!;
    }

    public void ApplyCurrentEdit()
    {
        CurrentNodeEditor?.FinishEdit();
        CurrentNodeEditor = null;
        //prevent from double OnEditContextChanged notification
        ProjectState.CurrentContextType = ProjectState.DefaultEditContextType;
        _appState.CurrentProject.CurrentEditedNode = null;
    }
}