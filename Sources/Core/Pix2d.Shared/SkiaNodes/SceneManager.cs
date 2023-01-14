using System;
using System.Collections.Generic;
using System.Linq;
using SkiaNodes.Abstract;
using SkiaNodes.TreeObserver;

namespace SkiaNodes
{
    public class SceneManager
    {
        private SKNode _root = new RootNode() {Name = "Root node", IsInteractive = true};
        private SKNode _scene;
        private static SceneManager _instance;

        public static SceneManager Current => _instance ??= new SceneManager();

        public event EventHandler SceneCreated;
        public event EventHandler SceneRemoved;
        public event EventHandler<StructureChangedEventArgs> SceneStructureChanged;


        public void SetScene(SKNode scene)
        {
            _root.Nodes.Clear();
            _scene = scene;
            _root.Nodes.Add(_scene);

            OnSceneCreated();
        }

        public void RemoveScene()
        {
            DisposeCurrentScene();
            _root.Nodes.Clear();
            _scene = null;

            OnSceneRemoved();
        }

        private void DisposeCurrentScene()
        {
            _scene?.Unload();
        }

        public IList<TContainer> GetCurrentSceneContainers<TContainer>()
            where TContainer : IContainerNode
        {
            var cs = GetCurrentScene();
            return cs?.Nodes.OfType<TContainer>().ToList();
        }

        private void OnSceneCreated()
        {
            SceneTreeObserver.Clear();
            SceneTreeObserver.SubscribeToStructureChanges(_scene, OnSceneStructureChangedCallback);
            SceneCreated?.Invoke(this, EventArgs.Empty);
        }

        private void OnSceneRemoved()
        {
            SceneTreeObserver.Clear();
            GC.Collect();
            SceneRemoved?.Invoke(this, EventArgs.Empty);
        }

        private void OnSceneStructureChangedCallback(StructureChangedEventArgs e)
        {
            SceneStructureChanged?.Invoke(this, e);
        }

        public SKNode GetCurrentScene()
        {
            return _scene;
        }

        public SKNode GetRootNode()
        {
            return _root;
        }
    }
}