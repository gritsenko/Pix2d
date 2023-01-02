using System;
using System.Collections.Generic;
using System.Linq;
using SkiaNodes;

namespace Pix2d.Primitives.Operations
{
    public static class SKNodeStatesExtensions
    {
        public static SKNodeState[] GetNodeStates(this IEnumerable<SKNode> nodes)
        {
            return BuildNodeStates(nodes).ToArray();
        }

        private static IEnumerable<SKNodeState> BuildNodeStates(IEnumerable<SKNode> nodes)
        { 
            foreach (var node in nodes.OrderBy(x => x.Index))
            {
                var state = new SKNodeState(node);
                state.StructureState = new SkNodeStructureState(node);
                state.TransformState = new SKNodeTransformState(node);
                yield return state;
            }
        }

        public static void ApplyStates(this IEnumerable<SKNode> nodes, IEnumerable<SKNodeState> states)
        {
            throw new NotImplementedException();
        }
        public static void ApplyStates(this IEnumerable<SKNodeState> states)
        {
            foreach (var state in states)
            {
                state.Apply();
            }
        }
    }
}