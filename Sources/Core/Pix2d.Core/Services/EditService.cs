#nullable enable
using System.Diagnostics;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
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

    private SKNode FrameEditorNode => _appState.CurrentProject.FrameEditorNode!;

    private SpriteEditor SpriteEditor => _spriteEditor ?? throw new InvalidOperationException("SpriteEditor is not initialized");


    public EditService(IViewPortRefreshService viewPortRefreshService,
        IViewPortService viewPortService,
        ISelectionService selectionService,
        AppState appState,
        IMessenger messenger,
        SpriteEditor spriteEditor)
    {
        _viewPortService = viewPortService;
        _viewPortRefreshService = viewPortRefreshService;
        _selectionService = selectionService;
        _appState = appState;
        _messenger = messenger;

        _appState.CurrentProject.FrameEditorNode = new FrameEditorNode() { ReparentMode = NodeReparentMode.Overflow };

        _spriteEditor = spriteEditor;


        _messenger.Register<ProjectLoadedMessage>(this, OnProjectLoadedMessage);
        _messenger.Register<NodesSelectedMessage>(this, OnNodesSelected);
    }

    private void OnNodesSelected(NodesSelectedMessage obj)
    {
        UpdateEditors();
    }

    private void OnProjectLoadedMessage(ProjectLoadedMessage message)
    {
        var scene = message.ActiveScene;

        if (scene.Nodes.FirstOrDefault() is Pix2dSprite sprite)
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