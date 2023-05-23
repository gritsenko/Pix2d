using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Primitives;

namespace Pix2d.Common
{
    public class SessionLogger
    {
        public IMessenger Messenger { get; }
        public List<OpLogItem> SessionOperationsLog = new List<OpLogItem>();
        public OpLogItem _lastItem = null;

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
            }
        }

        public static void InitInstance(IMessenger messenger)
        {
            Instance = new SessionLogger(messenger);
        }

        public static SessionLogger Instance { get; set; }

        public string GetSessionOperationLogText()
        {
            return string.Join("\n", Instance.SessionOperationsLog);
        }

        public static void OpLog(string info = null, [CallerMemberName] string caller = null)
        {
            if (info == null)
                info = "";
            else
                info = ": " + info;

            Instance.AddItemToOpLog(new OpLogItem(OperationEventType.Info, caller + info));
        }
        public static void OpLogCommand(string commandName = null)
        {
            Instance.AddItemToOpLog(new OpLogItem(OperationEventType.Command, commandName));
        }

    }
}
