using Pix2d.Abstract.Tools;

namespace Pix2d.Messages
{
    public class CurrentToolChangedMessage
    {
        public ITool OldTool { get; }
        public ITool NewTool { get; }

        public CurrentToolChangedMessage(ITool oldTool, ITool newTool)
        {
            NewTool = newTool;
            OldTool = oldTool;
        }
    }
}