namespace Pix2d.Messages;

public class ProjectCloseMessage
{

}

public class ShowMenuItemMessage
{
    public MenuItem ItemToShow { get; init; }
    public enum MenuItem
    {
        Licence,
    }
}