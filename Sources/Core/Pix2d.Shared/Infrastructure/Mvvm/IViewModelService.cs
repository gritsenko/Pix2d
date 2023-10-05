using System;

namespace Pix2d.Infrastructure.Mvvm;

public interface IViewModelService
{
    TViewModel GetViewModel<TViewModel>(bool singleton = true)
        where TViewModel : class;

    object GetViewModel(Type vmType, bool singleton = true);
}