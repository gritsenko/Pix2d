using System;
using System.Diagnostics;
using System.Linq;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Plugins.Sprite.Operations.Effects;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Common;
using SkiaSharp;

namespace Pix2d.Services;

public class EditService : IEditService
{
    private readonly SpriteEditor _spriteEditor;

    public IViewPortService ViewPortService { get; }
    public ISelectionService SelectionService { get; }
    public AppState AppState { get; }
    protected ProjectState ProjectState => AppState.CurrentProject;
    public IMessenger Messenger { get; }


    public EditContextType CurrentEditContextType
    {
        get => ProjectState.CurrentContextType;
        set
        {
            if (ProjectState.CurrentContextType != value)
            {
                ProjectState.CurrentContextType = value;
                OnEditContextChanged(ProjectState.CurrentContextType);
            }
        }
    }

    public INodeEditor CurrentNodeEditor
    {
        get => ProjectState.CurrentNodeEditor;
        set => ProjectState.CurrentNodeEditor = value;
    }

    public SKNode? CurrentEditedNode
    {
        get => ProjectState.CurrentEditedNode;
        private set
        {
            if (ProjectState.CurrentEditedNode != null)
                ProjectState.CurrentEditedNode.SizeChanged -= CurrentEditedNodeOnSizeChanged;

            ProjectState.CurrentEditedNode = value;

            if (ProjectState.CurrentEditedNode != null)
                ProjectState.CurrentEditedNode.SizeChanged += CurrentEditedNodeOnSizeChanged;

            OnCurrentEditNodeChanged(ProjectState.CurrentEditedNode);
        }
    }

    private void OnCurrentEditNodeChanged(SKNode currentEditedNode)
    {
        Messenger.Send(new EditedNodeChangedMessage(currentEditedNode));
    }

    private void CurrentEditedNodeOnSizeChanged(object sender, EventArgs e)
    {
        OnCurrentEditNodeChanged(ProjectState.CurrentEditedNode);
    }

    public EditContextType DefaultEditContextType { get; set; } = EditContextType.General;

    public SKNode FrameEditorNode => AppState.CurrentProject.FrameEditorNode;


    public EditService(IViewPortService viewPortService, ISelectionService selectionService, AppState appState, IMessenger messenger)
    {
        ViewPortService = viewPortService;
        SelectionService = selectionService;
        AppState = appState;
        Messenger = messenger;

        AppState.CurrentProject.FrameEditorNode = new FrameEditorNode() { ReparentMode = NodeReparentMode.Overflow };

        _spriteEditor = IoC.Create<SpriteEditor>();


        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoadedMessage);
        Messenger.Register<NodesSelectedMessage>(this, OnNodesSelected);
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
            RequestEdit(new SKNode[] {sprite});
            ViewPortService.ShowAll();
        }
    }

    private void UpdateEditors()
    {
        try
        {
            if (CurrentEditContextType == EditContextType.Sprite)
                return;

            var selection = ProjectState.Selection;
            if (selection == null || AppState.ToolsState.CurrentTool.ToolInstance is IDrawingTool)
            {
                FrameEditorNode.IsVisible = false;
                return;
            }

            FrameEditorNode.IsVisible = true;
            ((FrameEditorNode)FrameEditorNode).SetSelection(selection);
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(ProjectState.SceneNode);
            adornerLayer.Add(FrameEditorNode);
        }
        finally
        {
            ViewPortService.ViewPort.Refresh();
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
            SelectionService.SetActiveGroup(group);
            CurrentEditContextType = EditContextType.General;
        }

        if (node is Pix2dSprite)
        {
            CurrentEditedNode = node;
            _spriteEditor.SetTargetNode(node);
            CurrentNodeEditor = _spriteEditor;
            CurrentEditContextType = EditContextType.Sprite;
            OnEditorChanged();
        }

        if (node is TextNode)
        {
            CurrentEditContextType = EditContextType.Text;
        }
    }

    protected virtual void OnEditorChanged()
    {
        Messenger.Send(new NodeEditorChangedMessage());
    }

    private void OnEditContextChanged(EditContextType newContext)
    {
        Messenger.Send(new EditContextChangedMessage(newContext));
    }

    public void GroupNodes(SKNode[] nodes)
    {
        var parent = nodes[0].Parent;
        var newGroup = new GroupNode();
        newGroup.Name = "Group";
        foreach(var node in nodes)
        {
            newGroup.Nodes.Add(node);
        }
        parent.Nodes.Add(newGroup);
        newGroup.UpdateBoundsToContent();

        SelectionService.Select(newGroup);
    }

    public void UngroupNodes(GroupNode group)
    {
        foreach (var node in group.Nodes.ToArray())
        {
            group.Nodes.Remove(node);
            group.Parent.Nodes.Insert(group.Index, node);
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

        Messenger.Send(new CanvasSizeChanged());
    }

    public void CropCurrentSprite(SKRect newBounds)
    {
        if (!(GetCurrentEditor() is SpriteEditor editor)) return;

        editor.Crop(newBounds);
        UpdateEditors();
    }

    public INodeEditor GetCurrentEditor()
    {
        return CurrentNodeEditor;
    }

    public void AddEffect(ISKNodeEffect effect)
    {
        if (CurrentEditContextType == EditContextType.Sprite && CurrentNodeEditor is SpriteEditor spriteEditor)
            new AddEffectOperation(spriteEditor.CurrentSprite.SelectedLayer.Yield(), effect).Invoke();

        if (CurrentEditContextType == EditContextType.General)
            new AddEffectOperation(ProjectState.Selection.Nodes, effect).Invoke();
    }

    public void RemoveEffect(ISKNodeEffect effect)
    {
        if (CurrentNodeEditor is SpriteEditor spriteEditor)
            new RemoveEffectOperation(spriteEditor.CurrentSprite.SelectedLayer, effect).Invoke();
    }

    public void BakeEffect(ISKNodeEffect effect)
    {
        if (CurrentNodeEditor is SpriteEditor spriteEditor)
            new BakeEffectOperation(spriteEditor.CurrentSprite.SelectedLayer, effect).Invoke();
    }

    public void ApplyCurrentEdit()
    {
        CurrentNodeEditor?.FinishEdit();
        CurrentNodeEditor = null;
        //prevent from double OnEditContextChanged notification
        ProjectState.CurrentContextType = DefaultEditContextType;
        CurrentEditedNode = null;
        OnEditContextChanged(ProjectState.CurrentContextType);
    }
}