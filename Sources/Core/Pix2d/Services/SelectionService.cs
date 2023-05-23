using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Selection;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Common;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services
{
    public class SelectionService : ISelectionService
    {
        private ISceneService SceneService { get; }
        public ISnappingService SnappingService { get; }
        public IMessenger Messenger { get; }
        public AppState AppState { get; }
        protected ProjectState ProjectState => AppState.CurrentProject;

        public INodesSelection Selection
        {
            get => ProjectState.Selection;
            private set => ProjectState.Selection = value;
        }

        private SKNode Scene => SceneService.GetCurrentScene();


        // public event EventHandler SelectionUpdated;

        //public event EventHandler SelectionInvalidated;

        //public event EventHandler<SelectedNodesChangedEventArgs> SelectedNodesChanged;

        private IReadOnlyList<SKNode> SelectedNodes => Selection?.Nodes ?? Enumerable.Empty<SKNode>().ToArray();
        public bool HasSelectedNodes => SelectedNodes.Any();


        public GroupNode ActiveGroup { get; set; }

        public SelectionService(ISceneService sceneService, ISnappingService snappingService, IMessenger messenger, AppState appState)
        {
            SceneService = sceneService;
            SnappingService = snappingService;
            Messenger = messenger;
            AppState = appState;

            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        }

        private void OnOperationInvoked(OperationInvokedMessage obj)
        {
            if(obj.OperationType == OperationEventType.Redo || obj.OperationType == OperationEventType.Undo)
                Selection?.ResetFrame();
            Selection?.Invalidate();
        }

        public void Select(SKRect rect, bool addToSelection = false)
        {
            var nodes = Scene.GetVisibleDescendants(node =>
            {
                var bbox = node.GetBoundingBox();
                //if selection frame inside big object - don't select it
                if (bbox.Contains(rect)) return false;

                return !node.IsInLockedBranch() &&
                       bbox.IntersectsWith(rect);
            }, false).ToArray();

            if (nodes == null || !nodes.Any())
            {
                ClearSelection();
                return;
            }

            Select(nodes, addToSelection);
        }

        public void Select(SKPoint point, bool addToSelection = false)
        {
            var currentContainer = GetCurrentContainer();

            var nodesUnderPoint =
                currentContainer.Nodes.Where(x => x.IsVisible && !x.IsInLockedBranch() && x.ContainsPoint(point));
            //var nodesUnderPoint = Scene.GetVisibleDescendants(x => !x.DesignerState.IsLocked && x.ContainsPoint(point), false);
            if (!nodesUnderPoint.Any())
            {
                nodesUnderPoint = currentContainer.Yield();
            }

            var filteredNodes = FilterGroups(nodesUnderPoint);

            var nodeToSelect = filteredNodes.GetTopNode(x => !x.IsAdorner);

            if (nodeToSelect == null)
            {
                ClearSelection();
                return;
            }

            Select(new[] { nodeToSelect }, addToSelection);
        }

        private IEnumerable<SKNode> FilterGroups(IEnumerable<SKNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!(node.Parent is IGroupNode) || node.Parent == ActiveGroup)
                {
                    yield return node;
                }
            }
        }

        private void Select(SKNode[] nodes, bool addToSelection)
        {

            var intersection = SelectedNodes.Intersect(nodes);

            //shift pressed mode
            if (addToSelection)
            {
                //remove node from selection if it was selected already
                if (HasSelectedNodes && intersection.Any() && nodes.Length == 1)
                {
                    SetSelectedNodes(SelectedNodes.Except(nodes));
                    return;
                }

                //just add other nodes to selection
                if (HasSelectedNodes)
                {
                    SetSelectedNodes(SelectedNodes.Concat(nodes).Distinct());
                    return;
                }
            }

            SetSelectedNodes(nodes);
        }

        public void Select(params SKNode[] nodes)
        {
            if (nodes.SequenceEqual(SelectedNodes))
                return;

            SetSelectedNodes(nodes);
        }

        public void SelectNext(SKPoint point)
        {
            throw new NotImplementedException();
        }

        public void ClearSelection()
        {
            SetSelectedNodes(Enumerable.Empty<SKNode>());
            ActiveGroup = null;
        }

        public SKNode GetCurrentContainer()
        {
            return GetActiveContainer() as SKNode ?? Scene;
        }

        public IContainerNode GetActiveContainer()
        {
            if (HasSelectedNodes)
            {
                var n = SelectedNodes.OfType<DrawingContainerBaseNode>().FirstOrDefault();
                if (n != null)
                    return n;

                var container = SelectedNodes.GetParents().OfType<DrawingContainerBaseNode>().FirstOrDefault();
                return container;
            }

            var containers = SceneService.GetCurrentSceneContainers<DrawingContainerBaseNode>();
            if (containers?.Count == 1)
            {
                return containers[0];
            }

            return null;
        }

        public IContainerNode GetContainer(SKPoint worldPosition)
        {
            var container = Scene.Nodes.OfType<DrawingContainerBaseNode>().FirstOrDefault(x => x.GetBoundingBox().Contains(worldPosition));
            return container;
        }

        protected virtual void SetSelectedNodes(IEnumerable<SKNode> selectedNodes)
        {
            UpdateSelection(selectedNodes);

            //SelectedNodesChanged?.Invoke(this, new SelectedNodesChangedEventArgs() { Selection = Selection });

            //OnSelectionUpdated();
        }

        private void UpdateSelection(IEnumerable<SKNode> selectedNodes)
        {
            //old selection must be disposed to unsubscribe node events
            if (Selection != null)
            {
                Selection.Dispose();
            }

            Selection = new NodesSelection(selectedNodes, OnSelectionInvalidated, AspectLockProviderFunc);

            OnSelectionUpdated();
        }


        private bool AspectLockProviderFunc(SKNode[] nodes)
        {
            if (SnappingService.IsAspectLocked)
                return true;

            if (nodes.Length == 1 && nodes[0].DesignerState.LockAspect.HasValue)
            {
                return nodes[0].DesignerState.LockAspect.Value;
            }

            return false;
        }

        protected virtual void OnSelectionInvalidated()
        {
            //SelectionInvalidated?.Invoke(this, EventArgs.Empty);
            OnSelectionUpdated();
        }

        public void SetActiveGroup(GroupNode group)
        {
            ActiveGroup = group;
        }

        protected virtual void OnSelectionUpdated()
        {
            Messenger.Send(new NodesSelectedMessage());
        }
    }
}