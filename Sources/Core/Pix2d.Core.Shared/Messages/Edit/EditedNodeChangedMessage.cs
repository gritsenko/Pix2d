using SkiaNodes;

namespace Pix2d.Messages.Edit;

public class EditedNodeChangedMessage
{
    public SKNode Node { get; set; }

    public EditedNodeChangedMessage(SKNode node)
    {
        Node = node;
    }
}