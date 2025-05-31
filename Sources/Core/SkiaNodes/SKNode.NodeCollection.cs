using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaNodes.Common;
using SkiaSharp;

namespace SkiaNodes;

public partial class SKNode
{
    public SKNode()
    {
        Nodes = new NodeCollection(this);
    }

    private void UpdateNewChildNode(SKNode node, bool nodeTransformIsGlobal)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        if (node.Parent != null || nodeTransformIsGlobal)
        {
            GetGlobalTransform().TryInvert(out var invertedGlobal);
            var newTransform = default(SKMatrix);
            SKMatrix.Concat(ref newTransform, invertedGlobal, node.GetGlobalTransform());
            node.Position = new SKPoint(newTransform.TransX, newTransform.TransY);
        }

        node.Parent?.Nodes.Remove(node);

        node.Parent = this;
    }

    public class NodeCollection(SKNode hostNode) : IList<SKNode>
    {
        private readonly List<SKNode> _nodes = [];

        public Span<SKNode> AsSpan() => CollectionsMarshal.AsSpan(_nodes);

        public void Remove(IEnumerable<SKNode> nodes)
        {
            foreach (var node in nodes.ToArray())
                Remove(node);
        }

        public void CopyTo(SKNode[] array, int arrayIndex)
        {
            _nodes.CopyTo(array, arrayIndex);
        }

        public bool Remove(SKNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            try
            {
                _nodes.Remove(node);
                node.Parent = null;
                hostNode.OnChildrenRemoved(node.Yield());
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            return false;
        }

        public int Count => _nodes.Count;

        public bool IsReadOnly => false;


        public void Clear()
        {
            Remove(_nodes);
        }

        public bool Contains(SKNode node) => _nodes.Contains(node);

        public void AddRange(IEnumerable<SKNode> nodes)
        {
            lock (_nodes)
            {
                foreach (var node in nodes.ToArray())
                    Add(node);
            }
        }

        public void Add(SKNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            hostNode.UpdateNewChildNode(node, false);

            lock (_nodes)
            {
                _nodes.Add(node);
            }
            hostNode.OnChildrenAdded(node.Yield());
        }

        /// <summary>
        /// Add node without raising events and children recalculation 
        /// </summary>
        /// <param name="node">Node to add</param>
        public void AddInternal(SKNode node)
        {
            hostNode.UpdateNewChildNode(node, false);
            lock (_nodes)
            {
                _nodes.Add(node);
            }
        }

        public void Add(SKNode node, bool nodeTransformIsGlobal)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            hostNode.UpdateNewChildNode(node, nodeTransformIsGlobal);
            lock (_nodes)
            {
                _nodes.Add(node);
            }

            hostNode.OnChildrenAdded(node.Yield());
        }

        public int IndexOf(SKNode item)
        {
            lock (_nodes)
            {
                return _nodes.IndexOf(item);
            }
        }

        public int FindIndex(Predicate<SKNode> filter)
        {
            lock (_nodes)
            {
                return _nodes.FindIndex(filter);
            }
        }

        public void Insert(int index, SKNode node)
        {
            if (index == -1 || index >= _nodes.Count)
            {
                Add(node);
                return;
            }

            hostNode.UpdateNewChildNode(node, false);
            _nodes.Insert(index, node);
            hostNode.OnChildrenAdded(node.Yield());
        }

        public void Insert(int index, SKNode node, bool nodeTransformIsGlobal)
        {
            if (index == -1 || index >= _nodes.Count)
            {
                Add(node, nodeTransformIsGlobal);
                return;
            }

            hostNode.UpdateNewChildNode(node, nodeTransformIsGlobal);
            _nodes.Insert(index, node);
        }

        public void RemoveAt(int index)
        {
            var node = _nodes[index];
            _nodes.RemoveAt(index);
            hostNode.OnChildrenRemoved(node.Yield());
        }

        public SKNode this[int index]
        {
            get => index < Count ? _nodes[index] : null;
            set
            {
                _nodes[index] = value;
                hostNode.OnChildrenAdded(value.Yield());
            }
        }

        public bool Any()
        {
            return _nodes.Count > 0;
        }

        public IEnumerator<SKNode> GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public void RemoveFromParent()
    {
        Parent?.Nodes.Remove(this);
    }
}