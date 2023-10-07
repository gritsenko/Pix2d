using System.ComponentModel;
using System.Threading.Tasks;
using CommonServiceLocator;
using Mvvm;
using Pix2d.Abstract.Services;

namespace Pix2d.Mvvm;

[Bindable(true)]
public class Pix2dViewModelBase : ViewModelBase
{
    private static bool _designMode = false;

    public static bool IsDesignMode => _designMode;

    protected void RefreshViewPort()
    {
        var vp = ServiceLocator.Current.GetInstance<IViewPortService>().ViewPort;
        RunInUiThread(vp.Refresh);
    }

    public static void SetRuntimeMode()
    {
        _designMode = false;
    }

    public Task Load()
    {
        OnLoad();
        return OnLoadAsync();
    }

    protected virtual void OnLoad()
    {

    }

    protected virtual Task OnLoadAsync()
    {
        return Task.CompletedTask;
    }
}