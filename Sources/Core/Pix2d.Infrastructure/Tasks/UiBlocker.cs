using System.Collections.Concurrent;

namespace Pix2d.Infrastructure.Tasks;

public class UiBlocker : IDisposable
{
    private static readonly ConcurrentStack<string> _messageStack = new();
    private static Action<bool, string> _uiUpdater;

    public static void Initialize(Action<bool, string> uiUpdateAction)
    {
        _uiUpdater = uiUpdateAction;
    }

    public UiBlocker(string message)
    {
        _messageStack.Push(message);
        UpdateOverlay(visible: true, message);
    }

    public void Dispose()
    {
        _messageStack.TryPop(out _);

        if (_messageStack.TryPeek(out var previousMessage))
        {
            UpdateOverlay(visible: true, previousMessage);
        }
        else
        {
            UpdateOverlay(visible: false, string.Empty);
        }
    }

    private static void UpdateOverlay(bool visible, string message)
    {
        _uiUpdater?.Invoke(visible, message);
    }
}