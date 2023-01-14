using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Pix2d.Common
{
    public class DeferredAction
    {
        private static readonly Dictionary<string, DeferredAction> PendingActions =
            new Dictionary<string, DeferredAction>();

        public string Key { get; set; }
        private Action _action;
        private readonly Action<DeferredAction> _onActionComplete;

        private CancellationTokenSource _cts;

        public DeferredAction(int delayMilliseconds, string key, Action action, Action<DeferredAction> onActionComplete)
        {
            Key = key;
            _action = action;
            _onActionComplete = onActionComplete;

            _cts = new CancellationTokenSource();
            Execute(delayMilliseconds, _cts.Token);
        }

        public async void Execute(int delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _action?.Invoke();

                _onActionComplete?.Invoke(this);
            }
            catch (TaskCanceledException)
            {
                //ok
            }
        }

        public void Update(int delayMilliseconds, Action action)
        {
            _cts?.Cancel();

            _cts = new CancellationTokenSource();

            _action = action;
            Execute(delayMilliseconds, _cts.Token);
        }

        public static void Run(Action action) => Run(400, action);

        public static void Run(int delayMilliseconds, Action action, [CallerFilePath] string instanceKey = null)
        {
            DeferredAction inst;

            lock (PendingActions)
            {
                PendingActions.TryGetValue(instanceKey, out inst);
            }

            if (inst != null)
            {
                inst.Update(delayMilliseconds, action);
            }
            else
            {
                inst = new DeferredAction(delayMilliseconds, instanceKey, action, OnActionComplete);
                lock (PendingActions)
                {
                    PendingActions[instanceKey] = inst;
                }
            }

        }

        public static void OnActionComplete(DeferredAction action)
        {
            lock (PendingActions)
            {
                PendingActions.Remove(action.Key);
            }
        }
    }
}
