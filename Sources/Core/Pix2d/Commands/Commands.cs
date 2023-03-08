using Pix2d.Command;

namespace Pix2d;

public class Commands
{
    public static FileCommands File { get; } = new();
    public static ViewCommands View { get; } = new();
    public static EditCommands Edit { get; } = new();
    public static ProjectCommands Project { get; } = new();
    public static WindowCommands Window { get; } = new();
}