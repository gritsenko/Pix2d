using Pix2d.Abstract.Commands;

namespace Pix2d.Plugins.Collaborate.Commands;

internal class CollaborateCommands : CommandsListBase
{
    protected override string BaseName => "Collaborate";
    
    //public Pix2dCommand CaptureImage
    //    => GetCommand("Capture image from camera",
    //        new CommandShortcut(VirtualKeys.S),
    //        EditContextType.Sprite,
    //        () => ServiceLocator.Current.GetInstance<IImageCaptureService>().PasteImageAsync(),
    //        behaviour: DisableOnAnimation.Instance);
}