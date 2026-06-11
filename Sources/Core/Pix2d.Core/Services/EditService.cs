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
    // leak across scenes when switching projects.
    private SKNode FrameEditorNode =>
        _appState.CurrentProject.FrameEditorNode ??=
            new FrameEditorNode { ReparentMode = NodeReparentMode.Overflow };

    private SpriteEditor SpriteEditor => _spriteEditor ?? throw new InvalidOperationException("SpriteEditor is not initialized");


    public EditService(IViewPortRefreshService viewPortRefreshService,
        IViewPortService viewPortService,
        ISelectionService selectionService,
        AppState appState,
        IMessenger messenger,
        SpriteEditor spriteEditor,
        IOperationService operationService)
    {
        _viewPortService = viewPortService;
        _viewPortRefreshService = viewPortRefreshService;
        _selectionService = selectionService;
        _appState = appState;
        _messenger = messenger;
        _operationService = operationService;

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

        // If the active artboard was removed from the scene (e.g. undo of AddArtboard, or deleting a sprite),
        // fall back to a surviving artboard so the editor / drawing target never dangle on a detached node.
        var scene = _appState.CurrentProject.SceneNode;
        if (scene == null)
            return;

        var current = _appState.CurrentProject.CurrentEditedNode;
        if (current is Pix2dSprite && scene.Nodes.Contains(current))
            return;

        var survivor = scene.Nodes.OfType<Pix2dSprite>().FirstOrDefault();
        if (survivor != null)
            ActivateArtboard(survivor);
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

    private const float ArtboardGap = 16f;

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