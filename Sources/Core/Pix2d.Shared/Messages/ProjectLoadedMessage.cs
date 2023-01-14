using SkiaNodes;

namespace Pix2d.Messages
{
    public class ProjectLoadedMessage
    {
        public bool IsSessionMode;
        public SKNode ActiveScene;

        public ProjectLoadedMessage(SKNode activeScene, bool isSessionMode)
        {
            IsSessionMode = isSessionMode;
            ActiveScene = activeScene;
        }
    }
}