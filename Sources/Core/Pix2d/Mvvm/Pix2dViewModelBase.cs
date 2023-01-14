using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommonServiceLocator;
using Mvvm;
using Pix2d.Abstract;
using Pix2d.Primitives;

namespace Pix2d.Mvvm
{
    [Bindable(true)]
    public class Pix2dViewModelBase : ViewModelBase
    {
        private static bool _designMode = false;

        public static bool IsDesignMode => _designMode;

        protected static ICommand MapCommand(Pix2dCommand pix2dCommand, Action afterExecuteAction = null)
        {
            return new Pix2dCommandMapping(pix2dCommand, afterExecuteAction);
        }

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
}
