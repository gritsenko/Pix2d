#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Primitives;

namespace Pix2d.Common;

public class SessionLogger
{
    public static SessionLogger Instance { get; set; } = null!;

    public IMessenger Messenger { get; }
    public List<OpLogItem> SessionOperationsLog = [];
    private OpLogItem? _lastItem = null;

    public SessionLogger(IMessenger messenger)
    {
        Messenger = messenger;
        Init();
    }

    public void Init()
    {
        Messenger.Register<OperationInvokedMessage>(this, m =>
        {
            var newItem = new OpLogItem(m.Operation, m.OperationType);
            AddItemToOpLog(newItem);
        });
    }

    /// <summary>
    /// Upper bound on retained entries. The log used to grow for the whole session, which both leaks
    /// in a long drawing session and makes every read of it (crash summary, session crumb) cost more
    /// the longer the app has been open. Only the tail is ever of any diagnostic use.
    /// </summary>
    private const int MaxRetainedItems = 2000;

    public void AddItemToOpLog(OpLogItem newItem)
    {
#if DEBUG
        Debug.WriteLine("OpLog: " + newItem.Operation);
#endif
        if (_lastItem != null && _lastItem.Operation == newItem.Operation)
        {
            _lastItem.Count++;
        }
        else
        {
            SessionOperationsLog.Add(newItem);
            _lastItem = newItem;

            // Drop in chunks rather than one-per-add: RemoveRange from the front is O(n), so
            // trimming on every add past the cap would make a long session quadratic.
            if (SessionOperationsLog.Count > MaxRetainedItems + 256)
                SessionOperationsLog.RemoveRange(0, SessionOperationsLog.Count - MaxRetainedItems);
        }
    }

    public static void InitInstance(IMessenger messenger)
    {
        Instance = new SessionLogger(messenger);
    }

    public static string GetSessionOperationLogText() => string.Join("\n", Instance?.SessionOperationsLog ?? []);

    /// <summary>
    /// Joins only the last <paramref name="maxItems"/> entries. Used by the session crumb, which is
    /// rewritten every couple of seconds — building the full log text each time would make the write
    /// cost grow with session length for data that is then truncated anyway.
    /// </summary>
    public static string GetSessionOperationLogTail(int maxItems)
    {
        var log = Instance?.SessionOperationsLog;
        if (log == null || log.Count == 0)
            return string.Empty;

        // Snapshot the count once: entries are appended from the UI thread while the crumb may be
        // written from a background thread, and indexing past a shrinking list would throw.
        var count = Math.Min(log.Count, maxItems);
        var start = Math.Max(0, log.Count - count);
        var items = new string[count];
        for (var i = 0; i < count; i++)
            items[i] = log[start + i]?.ToString() ?? string.Empty;

        return string.Join("\n", items);
    }

    public static void OpLog(string? info = null, [CallerMemberName] string? caller = null)
    {
        if (info == null)
            info = "";
        else
            info = ": " + info;

        Instance.AddItemToOpLog(new OpLogItem(OperationEventType.Info, caller + info));
    }
    public static void OpLogCommand(string? commandName = null)
    {
        Instance.AddItemToOpLog(new OpLogItem(OperationEventType.Command, commandName ?? ""));
    }

}
