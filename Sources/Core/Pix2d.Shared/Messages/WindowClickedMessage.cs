namespace Pix2d.Messages;

public class WindowClickedMessage
{
    public WindowClickedMessage(object target)
    {
        Target = target;
    }

    public object Target { get; }
}