using CommonServiceLocator;
using Pix2d.Mvvm;

namespace Pix2d.Common;

public abstract class ViewBaseSingletonVm<TViewModel> : ViewBase<TViewModel>
    where TViewModel : class
{
    protected override void OnCreated() => ViewModel = GetViewModel<TViewModel>();

    protected T GetViewModel<T>(bool singleton = true) where T: class =>
        ServiceLocator.Current.GetInstance<IViewModelService>().GetViewModel<T>(true);

    protected ViewBaseSingletonVm() : base(default) //passing null to prevent immediate initialization
    {
    }
}