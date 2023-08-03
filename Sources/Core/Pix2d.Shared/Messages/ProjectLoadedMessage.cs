using SkiaNodes;

namespace Pix2d.Messages;

public class ProjectLoadedMessage
{
    /// <summary>
    /// True, if project was loaded from session file, that was automatically saved during last app run
    /// </summary>
    public bool LoadedFromLocalSession;

    /// <summary>
    /// Scene that will be set as the current
    /// </summary>
    public SKNode ActiveScene;

    public ProjectLoadedMessage(SKNode activeScene, bool loadedFromLocalSession)
    {
        LoadedFromLocalSession = loadedFromLocalSession;
        ActiveScene = activeScene;
    }
}