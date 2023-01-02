using System;
using System.Collections.Generic;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.State;
using Pix2d.Messages;
using Pix2d.State;
using SkiaNodes;
using SkiaNodes.Abstract;

namespace Pix2d.Services
{
    public class SceneService : ISceneService
    {
        public IAppState AppState { get; }
        public IMessenger Messenger { get; }

        public SceneService(IAppState appState, IMessenger messenger)
        {
            AppState = appState;
            Messenger = messenger;

            SKApp.SceneManager.SceneCreated += SceneManager_SceneCreated;
            Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        }

        private void SceneManager_SceneCreated(object sender, EventArgs e)
        {
            ((ProjectState)AppState.CurrentProject).SceneNode = SKApp.SceneManager.GetCurrentScene();
        }

        private void OnProjectLoaded(ProjectLoadedMessage message)
        {
            SKApp.SceneManager.SetScene(message.ActiveScene);
        }

        public SKNode GetRootNode()
        {
            return SKApp.SceneManager.GetRootNode();
        }

        public SKNode GetCurrentScene()
        {
            return SKApp.SceneManager.GetCurrentScene();
        }

        public IList<TContainer> GetCurrentSceneContainers<TContainer>() where TContainer : IContainerNode
        {
            return SKApp.SceneManager.GetCurrentSceneContainers<TContainer>();
        }
    }
}
