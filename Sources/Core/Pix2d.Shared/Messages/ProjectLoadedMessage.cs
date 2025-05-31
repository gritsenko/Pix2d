using SkiaNodes;

namespace Pix2d.Messages;

public class ProjectLoadedMessage(SKNode activeScene)
{
    /// <summary>
    /// Scene that will be set as the current
    /// </summary>
    public SKNode ActiveScene = activeScene;
}