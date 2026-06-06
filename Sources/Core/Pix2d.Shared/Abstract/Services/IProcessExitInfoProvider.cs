#nullable enable
using Pix2d.Primitives.Crash;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Optional capability for platform services that can report why the previous OS process of the
/// app terminated. Android implements this on top of <c>ActivityManager</c> (API 30+); other heads
/// don't and the crash report flow simply falls back to its launch-in-progress heuristic.
/// </summary>
public interface IProcessExitInfoProvider
{
    /// <summary>
    /// Returns details about the most recent termination of a previous app process, or <c>null</c>
    /// when the platform cannot provide it. Implementations must never throw.
    /// </summary>
    ProcessExitDetails? GetLastProcessExitDetails();
}
