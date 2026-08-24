using Pix2d.State;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Switches the active project among the open ones (desktop tabs). Keeps each project's
/// scene, undo history, viewport framing and editor target isolated from the others.
/// </summary>
public interface IProjectActivationService
{
    /// <summary>
    /// Makes <paramref name="target"/> (an already-loaded entry of AppState.LoadedProjects) the
    /// active project: saves the outgoing project's viewport framing, re-keys the undo history,
    /// swaps the scene, re-targets the editor and restores the target's framing. Does NOT send
    /// <c>ProjectLoadedMessage</c> — that message means "fresh load" and triggers heavier work.
    /// Sends <c>ProjectActivatedMessage</c> instead. No-op when already active.
    /// </summary>
    void ActivateProject(ProjectState target);

    /// <summary>
    /// First half of activation for a BRAND-NEW project (open/new into a tab): deactivates the
    /// outgoing project (viewport save, editor stop, autosave drain, history re-key) and makes
    /// <paramref name="newProject"/> current. The caller then runs the regular fresh-load path
    /// (sends <c>ProjectLoadedMessage</c> with the new scene).
    /// </summary>
    void BeginNewProjectActivation(ProjectState newProject);

    /// <summary>
    /// Moves <paramref name="target"/> to the head of <c>AppState.LoadedProjects</c> and activates
    /// it. This is the narrow-screen (phone) tab story: the strip only renders the tabs that fit,
    /// so a project picked from the overflow dropdown — or one activated while its tab is off the
    /// strip — has to be brought to the front to be visible at all. Sends
    /// <c>ProjectsListChangedMessage</c> for the reorder (which is what re-persists the tab order
    /// into <c>workspace.json</c>) before activating. No-op for an unknown project.
    /// </summary>
    void MoveProjectToFrontAndActivate(ProjectState target);
}
