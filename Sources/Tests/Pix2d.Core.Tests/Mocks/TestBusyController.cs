using Avalonia.Threading;
using Pix2d.Abstract.UI;

namespace Pix2d.Core.Tests.Mocks;

public class TestBusyController : IBusyController
{
    public bool IsBusy { get; }
    public async Task<bool> RunLongTaskAsync(Func<Task> task)
    {
        await task.Invoke();
        return true;
    }
}