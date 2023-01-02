namespace Pix2d.Command;

public class ProjectCommands : CommandsListBase
{
    protected override string BaseName => "Project";
    public string Publish => GetKey();
}