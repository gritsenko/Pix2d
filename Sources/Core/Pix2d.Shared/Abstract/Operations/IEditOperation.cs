using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Abstract.Operations
{
    public interface IEditOperation
    {
        bool AffectsNodeStructure { get; }
        void OnPerform();
        void OnPerformUndo();
        IEnumerable<SKNode> GetEditedNodes();
    }
}