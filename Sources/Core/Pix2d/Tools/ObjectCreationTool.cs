using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes.Controls;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Tools
{
    public abstract class ObjectCreationTool : BaseTool
    {
        private Frame _selectionFrame = new Frame();
        private SKNode _scene;

        public override string NextToolKey => nameof(ObjectManipulationTool);

        protected IObjectCreationService ObjectCreationService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IObjectCreationService>();
        protected IToolService ToolService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IToolService>();
        protected ISceneService SceneService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISceneService>();
        
        protected IEditService EditService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IEditService>();

        public override Task Activate()
        {
            _scene = SceneService.GetCurrentScene();
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(_scene);
            adornerLayer.Add(_selectionFrame);
            _selectionFrame.IsVisible = false;
            return base.Activate();
        }

        public override void Deactivate()
        {
            EditService.HideNodeEditor();
            base.Deactivate();
        }

        protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
            _selectionFrame.Position = e.Pointer.WorldPosition;
            _selectionFrame.SetSecondCornerPosition(_selectionFrame.Position);
            _selectionFrame.IsVisible = true;
        }

        protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
        {
            if (e.Pointer.IsPressed)
            {
                _selectionFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);
            }
        }

        protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
        {
            _selectionFrame.IsVisible = false;

            CreateObjectCore(_selectionFrame.GetBoundingBox());

            if (!string.IsNullOrWhiteSpace(NextToolKey))
            {
                ToolService.ActivateTool(NextToolKey);
            }
        }

        protected abstract void CreateObjectCore(SKRect destRect);
    }
}