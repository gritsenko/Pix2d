using Pix2d.Abstract.Commands;

namespace Pix2d.Plugins.Simple.Commands;

internal class SimpleCommands : CommandsListBase
{
    protected override string BaseName => "Sprite.Simple";
    
    //public Pix2dCommand CaptureImage
    //    => GetCommand("Capture image from camera",
    //        new CommandShortcut(VirtualKeys.S),
    //        EditContextType.Sprite,
    //        () => ServiceLocator.Current.GetInstance<IImageCaptureService>().PasteImageAsync(),
    //        behaviour: DisableOnAnimation.Instance);
}