using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Abstract.UI;
using Pix2d.Common;
using Pix2d.Drawing.Tools;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Mvvm;
using Pix2d.Tools;
using Pix2d.ViewModels.ToolSettings;

namespace Pix2d.ViewModels.ToolBar
{

    [Bindable(true)]
    public class ToolBarViewModel : Pix2dViewModelBase
    {
        private IToolService ToolService { get; }
        public IMenuController MenuController { get; }
        public IAppState AppState { get; }
        public IMessenger Messenger { get; }
        public IViewModelService ViewModelService { get; }

        private ITool CurrentTool => AppState.CurrentProject.CurrentTool;
        public bool CompactMode
        {
            get => Get<bool>();
            set => Set(value);
        }

        public ObservableCollection<ToolItemViewModel> Tools { get; } = new();

        public ToolItemViewModel SelectedToolItem
        {
            get => Get<ToolItemViewModel>();
            set => Set(value);
        }

        public ToolSettingsBaseViewModel SelectedToolSettings
        {
            get => Get<ToolSettingsBaseViewModel>();
            set => Set(value);
        }

        public EditContextType EditContextType => AppState.CurrentProject.CurrentContextType;

        [NotifiesOn(nameof(EditContextType))] public bool IsSpriteEditMode => EditContextType == EditContextType.Sprite;

        public static Dictionary<string, Type> SettingsVmMapping = new Dictionary<string, Type>()
        {
            {nameof(ObjectManipulationTool), typeof(ObjectManipulateToolViewModel)},
            {nameof(BrushTool), typeof(BrushToolSettingsViewModel)},
            {nameof(EraserTool), typeof(ToolSettingsBaseViewModel)},
            {nameof(FillTool), typeof(FillToolSettingsViewModel)},
            {nameof(PixelSelectTool), typeof(SelectionToolSettingsViewModel)},
            {nameof(EyedropperTool), typeof(ToolSettingsBaseViewModel)},
            //{nameof(VectorShapeTool), typeof(VectorShapeToolSettingsViewModel)},
            // {nameof(ShapeTool), typeof(ShapeToolSettingsViewModel)},
            //{nameof(ImageTool), typeof(ImageToolSettingsViewModel)},
            //{nameof(TextTool), typeof(ObjectManipulateToolViewModel)},
            //{nameof(ArtboardTool), typeof(ArtboardToolSettingsViewModel)}
        };

        public IRelayCommand SelectToolCommand => GetCommand<ToolItemViewModel>(OnSelectToolCommandExecute);

        public IRelayCommand ToggleToolSettingsCommand => GetCommand(() => ToggleSelectedToolSettings(SelectedToolItem));

        public ToolBarViewModel(IToolService toolService, IMenuController menuController, IAppState appState, IMessenger messenger,
            IViewModelService viewModelService)
        {
            if (IsDesignMode) return;

            ToolService = toolService;
            MenuController = menuController;
            AppState = appState;
            Messenger = messenger;
            ViewModelService = viewModelService;
            Messenger.Register<EditContextChangedMessage>(this, msg => UpdateToolsFromCurrentContext());
            Messenger.Register<CurrentToolChangedMessage>(this, ToolChanged);
            MenuController.SidebarModeChanged += MenuController_SidebarModeChanged;

            UpdateToolsFromCurrentContext(false);
        }

        private void MenuController_SidebarModeChanged(object sender, EventArgs e)
        {
            MenuController.ShowToolProperties = false;
            UpdateSelectedSettingsViewmodelCompactMode();
        }

        private void UpdateSelectedSettingsViewmodelCompactMode()
        {
            CompactMode = !MenuController.ShowSidebar;
            
            if(SelectedToolSettings != null)
                SelectedToolSettings.CompactMode = CompactMode;
        }

        private void OnSelectToolCommandExecute(ToolItemViewModel item)
        {
            SessionLogger.OpLog("Select " + item.ToolKey + " tool");

            if (CurrentTool.Key != item.ToolKey)
            {
                ToolService.ActivateTool(item.ToolKey);

                MenuController.ShowToolProperties = item.HasToolProperties;
            }
            else
            {
                ToggleSelectedToolSettings(item);
            }
        }

        private void ToggleSelectedToolSettings(ToolItemViewModel item)
        {
            if (item.HasToolProperties)
                MenuController.ShowToolProperties = !MenuController.ShowToolProperties;
            else
                MenuController.ShowToolProperties = false;
        }

        private void ToolChanged(CurrentToolChangedMessage message)
        {
            SelectedToolSettings?.Deactivated();

            foreach (var item in Tools)
            {
                item.IsSelected = item.ToolKey == CurrentTool.Key;

                if (item.IsSelected)
                    SelectedToolItem = item;
            }

            SelectedToolSettings = SelectedToolItem?.GetSettingsVm();
            UpdateSelectedSettingsViewmodelCompactMode();

            if (SelectedToolSettings != null)
            {
                SelectedToolSettings.Tool = CurrentTool;
                SelectedToolSettings.Activated();
            }

        }

        private void UpdateToolsFromCurrentContext(bool updateActiveTool = true)
        {
            OnPropertyChanged(nameof(EditContextType));

            Tools.Clear();

            var tools = ToolService.GetTools(EditContextType);

            foreach (var toolType in tools)
            {
                var toolSettingsVmType = GetViewModelType(toolType);
                
                Func<ToolSettingsBaseViewModel> callback = null;

                if (toolSettingsVmType != typeof(ToolSettingsBaseViewModel))
                    callback = () =>
                    {
                        var vm = (ToolSettingsBaseViewModel)ViewModelService.GetViewModel(toolSettingsVmType);
                        vm.ToolProviderFunc = () => ToolService.GetToolByKey(toolType.Name);
                        return vm;
                    };

                Tools.Add(new ToolItemViewModel(toolType.Name, callback));
            }

            if (updateActiveTool)
                ToolService.ActivateDefaultTool();

            SelectedToolItem = Tools.FirstOrDefault(x => x.ToolKey == CurrentTool?.Key);
            if (SelectedToolItem != null)
            {
                SelectedToolItem.IsSelected = false;
                SelectedToolItem.IsSelected = true;
            }

        }

        private static Type GetViewModelType(Type toolType) 
            => SettingsVmMapping.TryGetValue(toolType.Name, out var vmType) ? vmType : typeof(ToolSettingsBaseViewModel);
    }
}
