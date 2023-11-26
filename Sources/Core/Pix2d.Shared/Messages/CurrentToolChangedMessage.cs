using Pix2d.Abstract.Tools;

namespace Pix2d.Messages;

public class CurrentToolChangedMessage
{
    public ITool NewTool { get; }

    public CurrentToolChangedMessage(ITool newTool)
    {
        NewTool = newTool;
    }
}