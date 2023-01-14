using System;
using Mvvm.Messaging;
using Pix2d.Abstract.State;
using Pix2d.Common;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Mvvm;
using SkiaNodes.Interactive;

namespace Pix2d.ViewModels
{
    public class InfoPanelViewModel : Pix2dViewModelBase
    {
        public IAppState AppState { get; }
        public ISelectionState SelectionState => AppState.SelectionState;

        public string PointerInfo { get; set; } = "0, 0";
        public string SizeInfo { get; set; } = "0×0";

        public InfoPanelViewModel(IMessenger messenger, IAppState appState)
        {
            AppState = appState;
            SKInput.Current.PointerChanged += CurrentOnPointerChanged;
            messenger.Register<EditedNodeChangedMessage>(this, EditServiceOnCurrentEditedNodeChanged);
            messenger.Register<AppStateChangedMessage>(this, OnAppStateChanged);
        }

        private void OnAppStateChanged(AppStateChangedMessage msg)
        {
            if (msg.PropertyName == nameof(SelectionState.UserSelectingFrameSize) || msg.PropertyName == nameof(SelectionState.IsUserSelecting))
                UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            var size = SelectionState.IsUserSelecting ? SelectionState.UserSelectingFrameSize : AppState.CurrentProject.SelectionSize;
            SizeInfo = size.Width + "×" + size.Height;
            OnPropertyChanged(nameof(SizeInfo));
        }

        private void EditServiceOnCurrentEditedNodeChanged(EditedNodeChangedMessage message)
        {
            UpdateSelectionInfo();
        }

        private void CurrentOnPointerChanged(object sender, SKInputPointer pointer)
        {
            DeferredAction.Run(100,() =>
            {
                var pos = pointer.WorldPosition;
                PointerInfo = (int)pos.X + ", " + (int)pos.Y;
                OnPropertyChanged(nameof(PointerInfo));
            });
        }
    }
}
