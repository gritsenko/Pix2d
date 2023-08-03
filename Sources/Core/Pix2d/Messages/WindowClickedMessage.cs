namespace Pix2d.Messages;

public class WindowClickedMessage
{
    public WindowClickedMessage(StyledElement target)
    {
        Target = target;
    }

    public StyledElement Target { get; }
}