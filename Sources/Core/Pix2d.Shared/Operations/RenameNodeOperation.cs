using SkiaNodes;

namespace Pix2d.Operations;

/// <summary>
/// Undoable rename of one or more nodes. Used for layer titles; the artboard rename still goes
/// through <c>IArtboardObjectEditService.RenameAsync</c>, which renames a live scene object rather
/// than a document node in the sprite editor's history.
/// </summary>
public class RenameNodeOperation(IEnumerable<SKNode> nodes) : ChangeNodePropertyOperationBase<string>(nodes)
{
    protected override string GetValue(SKNode node) => node.Name;

    protected override void SetValue(SKNode node, string value) => node.Name = value;
}
