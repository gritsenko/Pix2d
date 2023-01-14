using Mvvm;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.Welcome
{
    public class WelcomeViewModel: Pix2dViewModelBase
    {
        public IPanelsController PanelsController { get; }

        public IRelayCommand StartCommand => GetCommand(() =>
        {
            PanelsController.OpenWorkSpace();
        });
        
        public WelcomeViewModel(IPanelsController panelsController)
        {
            PanelsController = panelsController;
        }
    }
}
