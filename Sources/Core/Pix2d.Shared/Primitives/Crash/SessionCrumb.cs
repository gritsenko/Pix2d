#nullable enable
namespace Pix2d.Primitives.Crash;

/// <summary>
/// A small, continuously-refreshed snapshot of "what the user was doing", persisted so it survives
/// the death of the process that wrote it.
/// <para>
/// It exists because a native crash or an ANR kills the app without any managed code running: the
/// crash is only discovered on the <b>next</b> launch, from the OS exit record. At that point the
/// in-memory context — last command, operation log, app state, and crucially the app <b>version</b>
/// that crashed — belongs to a brand-new process and describes nothing. Reading it from a fresh
/// process is what made recovered crash reports look empty; this file is the previous session
/// speaking for itself.
/// </para>
/// <para>
/// Written atomically (temp file + rename) because the process can be killed mid-write, and the one
/// launch that needs the crumb is exactly the launch after an abnormal death.
/// </para>
/// </summary>
public sealed class SessionCrumb
{
    /// <summary>Identifies the writing session, so a crumb is never mistaken for the current one.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// When the writing session started, epoch ms. Used to reject a crumb that cannot belong to the
    /// exit being reported — e.g. a crash so early that no crumb was written, which would otherwise
    /// pick up the session-before-last and attribute the wrong context to the death.
    /// </summary>
    public long StartedUtcMs { get; set; }

    /// <summary>When the crumb was last refreshed, epoch ms — how stale the context is.</summary>
    public long UpdatedUtcMs { get; set; }

    /// <summary>
    /// App version of the crashed session. The whole point of persisting it: reading the version
    /// from the recovering process attributes the crash to whatever is installed now, so a crash on
    /// the old build lands on the new release and the "did the fix work" comparison is poisoned.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string? LastCommandName { get; set; }

    /// <summary>Tail of the operation log — the actions leading into the crash.</summary>
    public string OpLogTail { get; set; } = string.Empty;

    /// <summary>Compact app-state snapshot (tool, canvas, frame, tabs) as of the last refresh.</summary>
    public string AppContext { get; set; } = string.Empty;
}
