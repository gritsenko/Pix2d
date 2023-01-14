using System;

namespace Mvvm.Messaging
{
    public class NotificationMessageAction : NotificationMessageWithCallback
    {
        public NotificationMessageAction(string notification, Action callback)
            : base(notification, callback)
        {
        }

        public NotificationMessageAction(object sender, string notification, Action callback)
            : base(sender, notification, callback)
        {
        }

        public NotificationMessageAction(object sender, object target, string notification, Action callback)
            : base(sender, target, notification, callback)
        {
        }

        public void Execute()
        {
            base.Execute();
        }
    }
}
