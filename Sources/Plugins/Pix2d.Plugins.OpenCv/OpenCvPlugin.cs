using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Desktop.Services;
using Pix2d.Infrastructure;
using Pix2d.Plugins.OpenCv.Commands;

namespace Pix2d.Plugins.OpenCv;

[ServiceProviderPlugin<IImageCaptureService, OpenCvCaptureService>]
[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(OpenCvPlugin))]
public class OpenCvPlugin(ICommandService commandService) : IPix2dPlugin
{

    public void Initialize()
    {

        commandService.RegisterCommandList(new OpenCvCommands());
    }
}