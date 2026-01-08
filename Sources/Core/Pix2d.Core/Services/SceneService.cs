using Pix2d.Messages;
using SkiaNodes;
using SkiaNodes.Abstract;

namespace Pix2d.Services;

public class SceneService : ISceneService
{
    public AppState AppState { get; }
    public IMessenger Messenger { get; }

    public SceneService(AppState appState, IMessenger messenger)
    {
        AppState = appState;
        Messenger = messenger;

        SKApp.SceneManager.SceneCreated += SceneManager_SceneCreated;
        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
    }

    private void SceneManager_SceneCreated(object? sender, EventArgs e)
    {
        AppState.CurrentProject.SceneNode = SKApp.SceneManager.GetCurrentScene();
    }

    private void OnProjectLoaded(ProjectLoadedMessage message)
    {
        SKApp.SceneManager.SetScene(message.ActiveScene);
    }

    public SKNode GetRootNode()
    {
        return SKApp.SceneManager.GetRootNode();
    }

    #pragma warning disable CS8766 // Nullability mismatch in return type
    public SKNode GetCurrentScene()
    #pragma warning restore CS8766 // Nullability mismatch in return type
    {
        return SKApp.SceneManager.GetCurrentScene()!;
    }

    public IList<TContainer> GetCurrentSceneContainers<TContainer>() where TContainer : IContainerNode
    {
        return SKApp.SceneManager.GetCurrentSceneContainers<TContainer>();
    }
}