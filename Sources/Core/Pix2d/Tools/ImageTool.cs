using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Tools;

namespace Pix2d.Tools;

[Pix2dTool(DisplayName = "Image tool")]
public class ImageTool : BaseTool
{
    protected IImportService ImportService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IImportService>();

    public override Task Activate()
    {

        ImportService.ImportToScene();

        return base.Activate();
    }
}