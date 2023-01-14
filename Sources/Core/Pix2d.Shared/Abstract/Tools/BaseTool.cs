using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Abstract.Tools
{
    public abstract class BaseTool : ITool
    {
        //public ISceneService SceneService { get; }
        //protected ISelectionService SelectionService => ServiceLocator.ServiceLocatorProvider().GetInstance<ISelectionService>();
        //protected IDrawingService DrawingService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IDrawingService>();

        //protected ISceneService SceneService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISceneService>();
        //protected IEditService EditService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IEditService>();

        protected SKNode RootNode => SKInput.Current.RootNodeProvider?.Invoke();
        public string Key => GetType().Name;
        public virtual EditContextType EditContextType => EditContextType.General;
        public bool IsActive { get; private set; }
        public bool IsEnabled { get; } = true;
        public virtual ToolBehaviorType Behavior => ToolBehaviorType.Selectable;
        public virtual string NextToolKey { get; } = "";
        public abstract string DisplayName { get; }
        public virtual string HotKey
        {
            get
            {
                if (DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ICommandService>()?.TryGetCommand("Tools.Activate" + this.Key, out var command) == true)
                {
                    return command.DefaultShortcut.Key.ToString();
                }
                return null;}
        }

        private bool _isPointerCatched = false;

        public virtual Task Activate()
        {
            Debug.WriteLine(Key + " Tool activated");
            IsActive = true;
            RootNode.PointerPressed += OnPointerPressed;
            RootNode.PointerReleased += OnPointerReleased;
            RootNode.PointerMoved += OnPointerMoved;
            RootNode.DoubleClicked += OnPointerDoubleClicked;
            return Task.CompletedTask;
        }

        public virtual void Deactivate()
        {
            if (_isPointerCatched)
                ReleasePointer();
            IsActive = false;
            Debug.WriteLine(Key + " Tool deactivated");
            RootNode.PointerPressed -= OnPointerPressed;
            RootNode.PointerReleased -= OnPointerReleased;
            RootNode.PointerMoved -= OnPointerMoved;
            RootNode.DoubleClicked -= OnPointerDoubleClicked;
        }

        protected virtual void OnPointerMoved(object sender, PointerActionEventArgs e)
        {
        }

        protected virtual void OnPointerReleased(object sender, PointerActionEventArgs e)
        {
        }

        protected virtual void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
        }

        protected virtual void OnPointerDoubleClicked(object sender, PointerActionEventArgs e)
        {
        }

        protected void CapturePointer()
        {
            SKInput.Current.CapturePointer(RootNode);
            _isPointerCatched = true;
        }

        protected void ReleasePointer()
        {
            SKInput.Current.ReleasePointer(RootNode);
            _isPointerCatched = false;
        }
    }
}