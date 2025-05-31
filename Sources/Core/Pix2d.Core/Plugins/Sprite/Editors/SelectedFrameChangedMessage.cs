namespace Pix2d.Plugins.Sprite.Editors;

public class SelectedFrameChangedMessage(int newFrameIndex, bool isPlaying)
{
    public bool IsPlaying { get; } = isPlaying;
    public int NewFrameIndex { get; } = newFrameIndex;
}