namespace Pix2d.Messages
{
    public class ProjectNameChangedMessage
    {
        public string NewName;

        public ProjectNameChangedMessage(string newName)
        {
            NewName = newName;
        }
    }
}