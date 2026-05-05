#nullable enable
namespace Pix2d.Abstract.Services;

/// <summary>
/// Public contract for the new incremental auto-save subsystem.
/// Replaces (and is a superset of) <see cref="ISessionService"/>.
/// </summary>
public interface IAutoSaveService
{
    /// <summary>True after <see cref="StartAsync"/> succeeded; false after <see cref="StopAsync"/>.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the periodic snapshot loop and tries to recover an orphaned session if any.</summary>
    Task StartAsync();

    /// <summary>Cooperatively stops the loop and flushes the last pending changes.</summary>
    Task StopAsync();

    /// <summary>
    /// Forces an immediate snapshot + commit and waits up to <paramref name="timeout"/>.
    /// Used on app shutdown / before "Open another project" / on critical events.
    /// </summary>
    Task ForceSaveAsync(TimeSpan timeout);

    /// <summary>
    /// Synchronous force-save designed for UI-thread lifecycle callbacks
    /// (Android <c>OnPause</c>, Avalonia <c>IActivatableLifetime.Deactivated</c>,
    /// desktop <c>Window.Closing</c>). MUST be called on the Avalonia UI thread.
    ///
    /// <para>
    /// Unlike <see cref="ForceSaveAsync"/>, this path runs drain + snapshot
    /// inline on the calling thread, then blocks ONLY on the file-I/O commit.
    /// That avoids the deadlock where a UI-thread <c>Wait</c> on a task that
    /// itself does <c>Dispatcher.UIThread.InvokeAsync</c> would freeze the app
    /// until the bounded timeout expires (which is exactly the "session lost
    /// on Android double-back" symptom).
    /// </para>
    /// </summary>
    void ForceSaveSync(TimeSpan timeout);

    /// <summary>
    /// Tries to recover the most recent orphaned session left over from a previous crash.
    /// Returns true on success.
    /// </summary>
    Task<bool> TryRecoverAsync();
}
