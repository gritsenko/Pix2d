using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Messages.ViewPort;
using Pix2d.Mvvm;
using SkiaNodes;

namespace Pix2d.ViewModels
{
    [Bindable(true)]
    public class ZoomBarViewModel : Pix2dViewModelBase
    {
        private ViewPort _viewPort;
        public IMessenger Messenger { get; }
        public IViewPortService ViewPortService { get; }

        public double MaxZoom
        {
            get => Get<double>();
            set => Set(value);
        }

        public double MinZoom
        {
            get => Get<double>();
            set => Set(value);
        }

        public double CurrentZoom { get; set; }

        public bool ZoomOutEnabled { get; set; }

        public string CurrentPercentZoom
        {
            get => Get<string>();
            set => Set(value);
        }

        public ICommand ZoomAllCommand => MapCommand(Commands.View.ZoomAll);
        public ICommand ZoomInCommand => MapCommand(Commands.View.ZoomIn);
        public ICommand ZoomOutCommand => MapCommand(Commands.View.ZoomOut);

        public ZoomBarViewModel(IMessenger messenger, IViewPortService viewPortService)
        {
            Messenger = messenger;
            ViewPortService = viewPortService;

            MaxZoom = ViewPort.MaxZoom;
            MinZoom = ViewPort.MinZoom;

            if (IsDesignMode)
                return;
            
            Messenger.Register<ViewPortInitializedMessage>(this, OnViewPortInitialized);
            Messenger.Register<ViewPortChangedViewMessage>(this, OnViewPortChanged);

            UpdateViewPort();
        }

        private void OnViewPortInitialized(ViewPortInitializedMessage obj)
        {
            UpdateViewPort();
        }

        private void UpdateViewPort()
        {
            _viewPort = ViewPortService.ViewPort;
            OnLoad();
        }

        private void OnViewPortChanged(ViewPortChangedViewMessage msg)
        {
            OnLoad();
        }

        protected override void OnLoad()
        {
            if (_viewPort == null) 
                return;
            
            CurrentZoom = _viewPort.Zoom;
            ZoomOutEnabled = CurrentZoom > 0.01;
            CurrentPercentZoom = (CurrentZoom * 100).ToString("###0") + "%";
        }
    }
}