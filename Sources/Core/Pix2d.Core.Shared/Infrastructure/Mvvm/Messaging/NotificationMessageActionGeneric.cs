using System;

namespace Mvvm.Messaging
{
    public class NotificationMessageAction<TCallbackParameter> : NotificationMessageWithCallback
    {
        public NotificationMessageAction(string notification, Action<TCallbackParameter> callback)
            : base(notification, callback)
        {
        }

        public NotificationMessageAction(object sender, string notification, Action<TCallbackParameter> callback)
            : base(sender, notification, callback)
        {
        }

        public NotificationMessageAction(
            object sender,
            object target,
            string notification,
            Action<TCallbackParameter> callback)
            : base(sender, target, notification, callback)
        {
        }

        public void Execute(TCallbackParameter parameter)
        {
            base.Execute(parameter);
        }
    }
}
