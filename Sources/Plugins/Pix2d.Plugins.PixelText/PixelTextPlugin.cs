using Pix2d.Abstract.Tools;
using Pix2d.Abstract;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Services;

namespace Pix2d.Plugins.PixelText;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(PixelTextPlugin))]
public class PixelTextPlugin(IToolService toolService) : IPix2dPlugin
{
    public void Initialize()
    {
        toolService.RegisterTool<PixelTextTool>(EditContextType.Sprite);
    }

}