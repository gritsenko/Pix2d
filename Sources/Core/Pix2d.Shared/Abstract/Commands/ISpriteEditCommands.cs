using Pix2d.Primitives;

namespace Pix2d.Abstract.Commands;

public interface ISpriteEditCommands : ICommandList
{
    Pix2dCommand CopyPixels { get; }
    Pix2dCommand CopyMerged { get; }
    Pix2dCommand CutPixels { get; }
    Pix2dCommand TryPaste { get; }
    Pix2dCommand CropPixels { get; }
    Pix2dCommand FlipHorizontal { get; }
    Pix2dCommand FlipVertical { get; }
    Pix2dCommand Rotate90 { get; }
    Pix2dCommand Rotate90All { get; }
    Pix2dCommand Clear { get; }
    Pix2dCommand Cancel { get; }
    Pix2dCommand ApplySelection { get; }
    Pix2dCommand SendLayerBackward { get; }
    Pix2dCommand BringLayerForward { get; }
    Pix2dCommand FillSelectionCommand { get; }
}