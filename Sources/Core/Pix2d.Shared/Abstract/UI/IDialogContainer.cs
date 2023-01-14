using System.Threading.Tasks;

namespace Pix2d.Abstract.UI
{
    public interface IDialogContainer
    {
        Task ShowDialogAsync(IDialogView dialog);
    }
}