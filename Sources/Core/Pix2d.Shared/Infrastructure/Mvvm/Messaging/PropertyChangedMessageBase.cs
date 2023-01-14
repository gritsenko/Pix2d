namespace Mvvm.Messaging
{
    public abstract class PropertyChangedMessageBase : MessageBase
    {
        protected PropertyChangedMessageBase(object sender, string propertyName)
            : base(sender)
        {
            PropertyName = propertyName;
        }

        protected PropertyChangedMessageBase(object sender, object target, string propertyName)
            : base(sender, target)
        {
            PropertyName = propertyName;
        }

        protected PropertyChangedMessageBase(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string PropertyName
        {
            get;
            protected set;
        }
    }
}
