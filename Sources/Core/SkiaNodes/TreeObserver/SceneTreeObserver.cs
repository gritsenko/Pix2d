using System;
using System.Collections.Generic;

namespace SkiaNodes.TreeObserver;

public class SceneTreeObserver
{
    private static readonly Dictionary<SKNode, HashSet<Action<StructureChangedEventArgs>>> _subscribers = new Dictionary<SKNode, HashSet<Action<StructureChangedEventArgs>>>();

    public static event EventHandler<NodesAddedEventArgs> NodesAdded;
    public static event EventHandler<NodesRemovedEventArgs> NodesRemoved;

    public static void OnNodesAdded(SKNode addedTo, IEnumerable<SKNode> addedNodes)
    {
            var e = new NodesAddedEventArgs(addedTo, addedNodes);
            NotifyStructureChanged(e);
            NodesAdded?.Invoke(addedTo, e);
        }

    public static void OnNodesRemoved(SKNode removedFrom, IEnumerable<SKNode> removedNodes)
    {
            var e = new NodesRemovedEventArgs(removedFrom, removedNodes);
            NotifyStructureChanged(e);
            NodesRemoved?.Invoke(removedFrom, e);
        }

    private static void NotifyStructureChanged(StructureChangedEventArgs e)
    {
            var changedNode = e.ParentNode;

            foreach (var subscriber in _subscribers)
            {
                var subscribedNode = subscriber.Key;

                if (subscribedNode == changedNode || changedNode.IsDescendantOf(subscribedNode))
                foreach (var callback in subscriber.Value)
                {
                    callback?.Invoke(e);
                }
            }
        }

    public static void SubscribeToStructureChanges(SKNode rootNode, Action<StructureChangedEventArgs> onStructureChangedCallback)
    {
            if (!_subscribers.TryGetValue(rootNode, out var nodeSubscribers))
            {
                nodeSubscribers = new HashSet<Action<StructureChangedEventArgs>>();
                _subscribers.Add(rootNode, nodeSubscribers);
            }

            nodeSubscribers.Add(onStructureChangedCallback);
        }

    public static void Clear()
    {
            _subscribers.Clear();
        }
}