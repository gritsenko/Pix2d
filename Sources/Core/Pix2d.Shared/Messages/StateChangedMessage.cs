namespace Pix2d.Messages;

public class StateChangedMessage
{
    public string PropertyName { get; set; }

    public StateChangedMessage(string propertyName = default)
    {
        PropertyName = propertyName;
    }
}