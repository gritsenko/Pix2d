using System;
using System.Threading.Tasks;

namespace Pix2d.Abstract.UI;

public interface IBusyController
{
    /// <summary>
    /// Is application busy with long running task
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Sets busy flag on application state and runs provided task, then sets busy flag to false
    /// </summary>
    /// <param name="task">Task to run</param>
    /// <returns>Returns true if task was completed successfully</returns>
    Task<bool> RunLongTaskAsync(Func<Task> task);
}