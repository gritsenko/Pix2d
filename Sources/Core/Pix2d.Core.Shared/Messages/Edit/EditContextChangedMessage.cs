using Pix2d.Abstract;

namespace Pix2d.Messages.Edit
{
    public class EditContextChangedMessage
    {
        public EditContextType NewContext { get; }

        public EditContextChangedMessage(EditContextType newContext)
        {
            NewContext = newContext;
        }
    }
}