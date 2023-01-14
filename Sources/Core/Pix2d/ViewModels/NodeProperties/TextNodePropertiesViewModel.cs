using Pix2d.CommonNodes;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.NodeProperties
{

    public class TextNodePropertiesViewModel : Pix2dViewModelBase
    {
        TextNode _textNode;
        public string Text
        {
            get => _textNode?.Text;
            set
            {
                _textNode.Text = value;
                OnPropertyChanged();
                RefreshViewPort();
            }
        }

        public TextNodePropertiesViewModel (TextNode textNode)
        {
            _textNode = textNode;
        }
    }

}