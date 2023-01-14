using System;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Tools;
using Pix2d.Tools;

namespace Pix2d.Plugins.Sprite
{
    public class ImageTool : BaseTool
    {
        protected IImportService ImportService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IImportService>();

        public override ToolBehaviorType Behavior => ToolBehaviorType.OneAction;

        public override string NextToolKey { get; } = nameof(ObjectManipulationTool);
        public override string DisplayName => "Image tool";

        public override Task Activate()
        {

            ImportService.ImportToScene();

            return base.Activate();
        }
    }
}