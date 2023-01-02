using System;
using Mvvm;

namespace Pix2d.Mvvm
{
    public interface IViewModelService
    {
        ViewModelBase GetViewModel(Type vmType, bool singleton = true);

        TViewModel GetViewModel<TViewModel>(bool singleton = true)
            where TViewModel : class;
    }
}