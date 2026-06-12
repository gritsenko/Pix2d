using Pix2d.State;

namespace Pix2d.Messages;

/// <summary>
/// Sent after switching the active project tab. Deliberately distinct from
/// <see cref="ProjectLoadedMessage"/>, which triggers fresh-load-only work
/// (undo history clear, sprite activation + ShowAll, grid reset).
/// </summary>
public class ProjectActivatedMessage(ProjectState project)
{
    public ProjectState Project { get; } = project;
}
