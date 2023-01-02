using System;

namespace Mvvm.Messaging
{
    public class NotificationMessageWithCallback : NotificationMessage
    {
        private readonly Delegate callback;

        public NotificationMessageWithCallback(string notification, Delegate callback)
            : base(notification)
        {
            CheckCallback(callback);
            this.callback = callback;
        }

        public NotificationMessageWithCallback(object sender, string notification, Delegate callback)
            : base(sender, notification)
        {
            CheckCallback(callback);
            this.callback = callback;
        }

        public NotificationMessageWithCallback(object sender, object target, string notification, Delegate callback)
            : base(sender, target, notification)
        {
            CheckCallback(callback);
            this.callback = callback;
        }

        public virtual object Execute(params object[] arguments)
        {
            return callback.DynamicInvoke(arguments);
        }

        private static void CheckCallback(Delegate callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback", "Callback may not be null");
            }
        }
    }
}
