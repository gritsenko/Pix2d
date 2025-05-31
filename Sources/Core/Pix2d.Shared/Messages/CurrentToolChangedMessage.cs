using Pix2d.Abstract.Tools;

namespace Pix2d.Messages;

public class CurrentToolChangedMessage(ITool newTool)
{
    public ITool NewTool { get; } = newTool;
}