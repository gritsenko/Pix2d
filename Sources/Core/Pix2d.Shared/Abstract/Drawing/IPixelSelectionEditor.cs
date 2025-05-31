using Pix2d.Primitives.Edit;

namespace Pix2d.Abstract.Drawing;

public interface IPixelSelectionEditor
{
    bool HasSelection { get; }
    void RotateSelection(int angle);
    void FlipSelection(FlipMode mode);
}