namespace Pix2d.Messages
{
    public class AppStateChangedMessage
    {
        public string PropertyName { get; set; }

        public AppStateChangedMessage(string propertyName = default)
        {
            PropertyName = propertyName;
        }
    }
}
