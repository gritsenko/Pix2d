using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Plugins.Collaborate.Commands;

namespace Pix2d.Plugins.Collaborate;

public class CollaboratePlugin(IDrawingService drawingService, IToolService toolService, ICommandService commandService, IMessenger messenger)
    : IPix2dPlugin
{
    public void Initialize()
    {
        commandService.RegisterCommandList(new CollaborateCommands());
        messenger.Register<OperationInvokedMessage>(this, m =>
        {

        });
        //toolService.RegisterTool<YourTool>(EditContextType.Sprite);
    }
}