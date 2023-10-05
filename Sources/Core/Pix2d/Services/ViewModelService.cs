using System;
using System.Collections.Generic;
using Pix2d.Infrastructure.Mvvm;
using Pix2d.Mvvm;

namespace Pix2d.Services;

public class ViewModelService : IViewModelService
{

    private readonly HashSet<Type> SingletonVms = new();

    public TViewModel GetViewModel<TViewModel>(bool singleton = true)
        where TViewModel : class
        => GetViewModel(typeof(TViewModel), singleton) as TViewModel;

    public object GetViewModel(Type vmType, bool singleton = true)
    {
        try
        {
            lock (SingletonVms)
            {
                if (singleton && !SingletonVms.Contains(vmType))
                {
                    SingletonVms.Add(vmType);
                    IoC.Get<SimpleContainer>().RegisterSingleton(vmType, vmType);
                }
            }

            var vm = IoC.GetInstance(vmType, null);

            if (vm is Pix2dViewModelBase vmLoadable)
            {
                vmLoadable.Load();
            }

            return vm;

        }
        catch (Exception e)
        {
            Logger.LogException(e);
            throw;
        }
    }

}