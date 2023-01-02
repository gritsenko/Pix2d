namespace Mvvm.Messaging
{
    public class PropertyChangedMessage<T> : PropertyChangedMessageBase
    {
        public PropertyChangedMessage(object sender, T oldValue, T newValue, string propertyName)
            : base(sender, propertyName)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public PropertyChangedMessage(T oldValue, T newValue, string propertyName)
            : base(propertyName)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public PropertyChangedMessage(object sender, object target, T oldValue, T newValue, string propertyName)
            : base(sender, target, propertyName)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public T NewValue
        {
            get;
            private set;
        }

        public T OldValue
        {
            get;
            private set;
        }
    }
}
