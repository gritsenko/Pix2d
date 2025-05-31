namespace Pix2d.Abstract.NodeTypes;

public interface IAnimatedNode
{
    float FrameRate { get; }
    int CurrentFrameIndex { get; }

    int SelectedLayerIndex { get; set; }

    int GetFramesCount();
    void SetFrameIndex(int index);
}