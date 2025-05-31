using Pix2d.CommonNodes;

namespace Pix2d.Abstract.Edit;

public interface ISpriteEditor : INodeEditor
{
    Pix2dSprite CurrentSprite { get; }
    int CurrentFrameIndex { get; }
}