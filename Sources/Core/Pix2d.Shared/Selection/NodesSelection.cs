#nullable enable
using Pix2d.CommonNodes;
using Pix2d.Operations;
using Pix2d.Primitives.Edit;
using Pix2d.Primitives.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Abstract.Selection;

public class NodesSelection : INodesSelection
{
    public Func<SKNode[], bool> AspectLockProviderFunc { get; }

    public event EventHandler Invalidated;

    private readonly Action _onInvalidatedCallback;
    private TransformOperation _editOperation;

    public bool GenerateOperations { get; set; } = true;

    private SKRect LocalBounds { get; set; }
    public SKRect Bounds { get; private set; }
    public SKNode[] Nodes { get; set; }

    public float X
    {
        get => Frame.Position.X;
        set => SetPosition(new SKPoint(value, Y));
    }

    public float Y
    {
        get => Frame.Position.Y;
        set => SetPosition(new SKPoint(X, value));
    }

    public float Width
    {
        get => Frame.Size.Width;
        set => SetSize(value, GetNewHeight(value, LockAspect));
    }

    public float Height
    {
        get => Frame.Size.Height;
        set => SetSize(GetNewWidth(value, LockAspect), value);
    }

    public float Rotation
    {
        get => Frame.Rotation;
        set => SetRotation(value);
    }

    public string Name
    {
        get => Nodes.FirstOrDefault()?.Name ?? "";
        set
        {
            if (Nodes?.Any() == true)
                Nodes[0].Name = value;
        }
    }

    public float Opacity
    {
        get => Nodes.FirstOrDefault()?.Opacity * 100 ?? 0;
        set
        {
            if (Nodes?.Any() == true)
            {
                Nodes[0].Opacity = value / 100;
                Invalidate();
            }
        }
    }

    public bool IsVisible
    {
        get => Nodes.FirstOrDefault()?.IsVisible ?? true;
        set
        {
            if (Nodes?.Any() == true)
            {
                Nodes[0].IsVisible = value;
                Invalidate();
            }
        }
    }

    public bool LockAspect => AspectLockProviderFunc?.Invoke(Nodes) ?? false;

    public string Path { get; set; }

    public NodeExportMode ExportMode
    {
        get => Nodes?.Any() == true ? Nodes[0].DesignerState.ExportSettings.ExportMode : NodeExportMode.Export;
        set => Nodes[0].DesignerState.ExportSettings.ExportMode = value;
    }

    public float ExportScale
    {
        get => Nodes?.Any() == true ? Nodes[0].DesignerState.ExportSettings.ExportScale : 1;
        set
        {
            foreach (var node in Nodes)
                node.DesignerState.ExportSettings.ExportScale = value;
        }
    }

    public NodeExportFormat ExportFormat
    {
        get => Nodes?.Any() == true ? Nodes[0].DesignerState.ExportSettings.ExportFormat : NodeExportFormat.Png;
        set
        {
            foreach (var node in Nodes)
                node.DesignerState.ExportSettings.ExportFormat = value;
        }
    }

    public int NodesCount => Nodes.Length;
    public SKNode Frame { get; set; }


    public NodesSelection(
        IEnumerable<SKNode> selectedNodes,
        Action onInvalidatedCallback,
        Func<SKNode[], bool>? aspectLockProviderFunc = null)
    {
        AspectLockProviderFunc = aspectLockProviderFunc;
        _onInvalidatedCallback = onInvalidatedCallback;
        Nodes = selectedNodes.ToArray();

        SubscrubeToNodeEvents(Nodes);
        Invalidate(false);
    }


    public void SetPosition(SKPoint newPos)
    {
        var delta = newPos - Frame.Position;
        if (delta == SKPoint.Empty)
            return;

        Frame.Position = newPos;

        foreach (var node in Nodes)
            node.Position += delta;

        Invalidate();
    }

    public void SetPivotPosition(SKPoint newPos)
    {
        var delta = newPos - Frame.PivotPosition;
        if (delta == SKPoint.Empty) return;

        Frame.PivotPosition = newPos;
        foreach (var node in Nodes)
        {
            node.PivotPosition += delta;
        }

        Invalidate();
    }

    public void SetSize(SKSize newSize) => SetSize(newSize.Width, newSize.Height);

    public void SetSize(float newW, float newH)
    {
        var delta = new SKPoint(newW - Width, newH - Height);

        Frame.Size = new SKSize(newW, newH);

        foreach (var node in Nodes)
            node.Size = new SKSize(node.Size.Width + delta.X, node.Size.Height + delta.Y);

        Invalidate();
    }

    public void SetRotation(float angleDegrees)
    {
        var delta = angleDegrees - Frame.Rotation;

        Frame.Rotation = angleDegrees;

        foreach (var node in Nodes)
        {
            if (node.Size == Frame.Size)
            {
                node.PivotPosition = Frame.PivotPosition;
                node.Position = Frame.Position;
            }
            node.Rotation += delta;
        }

        Invalidate();
    }


    private void CalculateDimensions()
    {
        if (!Nodes.Any())
            return;

        Bounds = Nodes.GetBounds();
        //We need Local position relative to artboard of selected items
        var container = Nodes[0].GetParentsChain().OfType<DrawingContainerBaseNode>().FirstOrDefault();
        if (container != null)
        {
            var localPos = container.GetLocalPosition(Bounds.Location);
            LocalBounds = new SKRect(localPos.X, localPos.Y, localPos.X + Bounds.Width, localPos.Y + Bounds.Height);
        }
        else
        {
            LocalBounds = Bounds;
        }
    }

    public void Invalidate(bool raiseNotificationEvents = true)
    {
        CalculateDimensions();
        UpdateFrame();
        UpdatePath();
        //SetNodesDirty();

        if (raiseNotificationEvents)
        {
            _onInvalidatedCallback?.Invoke(); //callback for selection service
            OnInvalidated(); //event for consumers (not sure how good this solution is)
        }
    }

    public void ResetFrame()
    {
        Frame = null;
    }

    private void UpdateFrame()
    {
        if (Frame != null)
            return;

        Frame = new SKNode() { Name = "Selection frame" };
        Frame.Size = LocalBounds.Size;
        Frame.PivotPosition = new SKPoint(Frame.Size.Width / 2, Frame.Size.Height / 2);
        Frame.Position = new SKPoint(LocalBounds.MidX, LocalBounds.MidY);
        Frame.Rotation = 0;
    }

    private void UpdatePath()
    {
        Path = "Nothing selected";
        if (Nodes.Length > 1)
        {
            Path = "Several nodes selected";
        }
        else if (Nodes.Length == 1)
        {
            foreach (var node in Nodes)
            {
                var chain = node.GetParentsChain().Skip(2);
                Path = "/" + string.Join("/", chain.Select(x => x.DisplayName));
            }
        }
    }

    private void SetNodesDirty()
    {
        foreach (var node in Nodes)
        {
            node.SetDirty();
        }
    }

    public void UpdateParents(NodeReparentMode reparentMode)
    {
        if (reparentMode == NodeReparentMode.None)
            return;

        throw new NotImplementedException("It's general mode operation, not implemented yet");
        //var artboards = SceneService.GetCurrentSceneContainers<ArtboardNode>();
        //var scene = SceneService.GetCurrentScene();

        //if (reparentMode == NodeReparentMode.Overflow)
        //{
        //    foreach (var node in Nodes)
        //    {
        //        ReparentIfOverflow(node, artboards, scene);
        //    }
        //}
    }

    private static bool ReparentIfOverflow(SKNode node, IList<ArtboardNode> artboards, SKNode scene)
    {
        if (node is ArtboardNode)
            return false;

        if (node.Parent is IGroupNode)
            return false;

        var nodeBounds = node.GetBoundingBox();

        foreach (var artboard in artboards)
        {
            var artboardBounds = artboard.GetBoundingBox();
            if (artboardBounds.IntersectsWithInclusive(nodeBounds) || artboardBounds.Contains(nodeBounds))
            {
                if (node.Parent != artboard)
                {
                    artboard.Nodes.Add(node, true);
                }

                return true;
            }
        }

        if (node.Parent != scene)
        {
            scene.Nodes.Add(node);
            return true;
        }

        return false;
    }

    public void SendBackward()
    {
        foreach (var node in Nodes.OrderBy(x => x.Index))
        {
            var parent = node.Parent;
            var index = node.Index;

            var prevNodeIndex = index - 1;
            if (prevNodeIndex < 0)
            {
                return;
            }
            var prevNode = parent.Nodes[prevNodeIndex];
            node.RemoveFromParent();

            var newIndex = prevNode.Index;
            parent.Nodes.Insert(newIndex, node);
        }

        Invalidate();
    }

    public bool CanBringForward(SKNode node)
    {
        return node.Index + 1 < node.Parent.Nodes.Count;
    }

    public void BringForward()
    {
        foreach (var node in Nodes.OrderBy(x => x.Index))
        {
            if (!CanBringForward(node))
                continue;

            var parent = node.Parent;
            var index = node.Index;

            if (node.Index >= parent.Nodes.Count)
                return;

            var nextNodeIndex = index + 1;
            var nextNode = parent.Nodes[nextNodeIndex];
            node.RemoveFromParent();

            var newIndex = nextNode.Index + 1;
            parent.Nodes.Insert(newIndex, node);
        }

        Invalidate();
    }

    public void Delete()
    {
        foreach (var node in Nodes)
        {
            node.RemoveFromParent();
        }

        Nodes = new SKNode[0];
        Invalidate();
    }

    public void Duplicate()
    {
        var listNewNodes = new SKNode[Nodes.Length];

        for (var i = 0; i < Nodes.Length; i++)
        {
            var node = Nodes[i];
            var newNode = node.Clone();
            listNewNodes[i] = newNode;

            if (node is ArtboardNode)
            {
                newNode.Position = new SKPoint(node.Position.X + node.Size.Width + 30, node.Position.Y);

                throw new NotImplementedException("It's general mode operation, not implemented yet");

                //SceneService.GetCurrentScene().Nodes.Insert(node.Index + 1, newNode, true);

                newNode.SetDirty();
                continue;
            }

            node.Parent.Nodes.Insert(node.Index + 1, newNode);
        }

        Nodes = listNewNodes.ToArray();
        Invalidate();
    }

    public void MoveBy(int dx, int dy)
    {
        var offset = new SKPoint(dx, dy);
        foreach (var node in Nodes)
        {
            node.Position += offset;
        }

        Invalidate();
    }

    public void AlignVertically(VerticalAlignment verticalAlignment)
    {
        switch (verticalAlignment)
        {
            case VerticalAlignment.Top:
                Nodes.AlignToTop();
                break;
            case VerticalAlignment.Center:
                Nodes.AlignToCenterVertically();
                break;
            case VerticalAlignment.Bottom:
                Nodes.AlignToBottom();
                break;
            case VerticalAlignment.Stretch:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(verticalAlignment), verticalAlignment, null);
        }
        Invalidate();
    }

    public void AlignHorizontally(HorizontalAlignment horizontalAlignment)
    {
        switch (horizontalAlignment)
        {
            case HorizontalAlignment.Left:
                Nodes.AlignToLeft();
                break;
            case HorizontalAlignment.Center:
                Nodes.AlignToCenterHorizontally();
                break;
            case HorizontalAlignment.Right:
                Nodes.AlignToRight();
                break;
            case HorizontalAlignment.Stretch:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(horizontalAlignment), horizontalAlignment, null);
        }
        Invalidate();
    }

    private float GetNewHeight(float newWidth, bool lockAspect)
    {
        if (!lockAspect)
            return Height;

        var aspect = Width / Height;
        return newWidth / aspect;
    }

    private float GetNewWidth(float newHeight, bool lockAspect)
    {
        if (!lockAspect)
            return Width;

        var aspect = Width / Height;
        return newHeight * aspect;
    }

    protected virtual void OnInvalidated()
    {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        foreach (var node in Nodes)
        {
            node.IsVisible = false;
        }
        Invalidate();
    }

    public void InitOperation<TOperation>() where TOperation : TransformOperation
    {
        if (!GenerateOperations)
            return;

        if (typeof(TOperation) == typeof(MoveOperation))
        {
            _editOperation = new MoveOperation(Nodes);
        }

        if (typeof(TOperation) == typeof(ResizeOperation))
        {
            _editOperation = new ResizeOperation(Nodes);
        }

        if (typeof(TOperation) == typeof(RotateOperation))
        {
            _editOperation = new RotateOperation(Nodes);
        }
    }

    public void FinishOperation()
    {
        if (!GenerateOperations)
            return;

        InvalidateGroups();
        _editOperation.SetFinalData();
        //_editOperation.PushToHistory();
        throw new NotImplementedException("You must pass operation service to selection");
    }

    private void InvalidateGroups()
    {
        foreach (var node in Nodes)
        {
            if (node.Parent is GroupNode group)
            {
                group.UpdateBoundsToContent();
            }
        }
    }

    private void SubscrubeToNodeEvents(SKNode[] nodes)
    {
        foreach (var node in nodes.ToArray())
        {
            node.NodeInvalidated += Node_NodeInvalidated;
        }
    }

    private void Node_NodeInvalidated(object sender, EventArgs e)
    {
        //Invalidate();
    }

    private void UnsubscrubeFromNodeEvents(SKNode[] nodes)
    {
        foreach (var node in nodes.ToArray())
        {
            node.NodeInvalidated -= Node_NodeInvalidated;
        }
    }

    public void Dispose()
    {
        UnsubscrubeFromNodeEvents(Nodes);
    }
}