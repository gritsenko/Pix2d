using Pix2d.Abstract.Selection;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface ISelectionService
{
    bool HasSelectedNodes { get; }
    INodesSelection Selection { get; }

    void Select(params SKNode[] node);
    void Select(SKRect rect, bool addToSelection = false);
    void Select(SKPoint point, bool addToSelection = false);
    void SelectNext(SKPoint point);

    void ClearSelection();
    IContainerNode GetActiveContainer();
    IContainerNode GetContainer(SKPoint worldPosition);
    void SetActiveGroup(GroupNode group);
}