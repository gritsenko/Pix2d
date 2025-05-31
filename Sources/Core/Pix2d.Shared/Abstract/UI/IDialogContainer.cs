using System.Threading.Tasks;

namespace Pix2d.Abstract.UI;

public interface IDialogContainer
{
    event EventHandler CloseButtonClicked;
    void ShowDialog(IDialogView dialog);
    void CloseDialog();
}