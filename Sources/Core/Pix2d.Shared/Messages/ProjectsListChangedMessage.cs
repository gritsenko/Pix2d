namespace Pix2d.Messages;

/// <summary>
/// Sent when AppState.LoadedProjects gains or loses an entry (tab opened / closed).
/// The list itself stays a plain List, so UI rebuilds from this signal.
/// </summary>
public class ProjectsListChangedMessage
{
    public static readonly ProjectsListChangedMessage Default = new();
}
