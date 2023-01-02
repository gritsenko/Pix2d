using Pix2d.Command;

namespace Pix2d;

public class Commands
{
    public static FileCommands File { get; } = new FileCommands();
    public static ViewCommands View { get; } = new ViewCommands();
    public static EditCommands Edit { get; } = new EditCommands();
    public static ProjectCommands Project { get; } = new ProjectCommands();
}