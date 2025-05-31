using Pix2d.Abstract.Operations;

namespace Pix2d.Primitives.Drawing;

public class SelectionTransformedEventArgs(IEditOperation operation) : EventArgs
{
    public IEditOperation Operation { get; } = operation;
}