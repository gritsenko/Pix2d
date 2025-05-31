using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Operations;

public abstract class EditOperationBase : IEditOperation
{
    public virtual bool AffectsNodeStructure { get; protected set; }
    public abstract void OnPerform();
    public abstract void OnPerformUndo();
    public abstract IEnumerable<SKNode> GetEditedNodes();
}