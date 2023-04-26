using System;
using Mvvm;

namespace Pix2d.Mvvm;

public interface IViewModelService
{
    TViewModel GetViewModel<TViewModel>(bool singleton = true)
        where TViewModel : class;

    object GetViewModel(Type vmType, bool singleton = true);
}