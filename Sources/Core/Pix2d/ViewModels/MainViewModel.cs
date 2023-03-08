using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Abstract.UI;
using Pix2d.CommonNodes;
using Pix2d.Drawing.Tools;
using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite;
using Pix2d.ViewModels.Color;
using Pix2d.ViewModels.Export;
using Pix2d.ViewModels.MainMenu;
using Pix2d.ViewModels.ToolBar;
using SkiaSharp;

namespace Pix2d.ViewModels;

public class MainViewModel : Pix2dViewModelBase, IMenuController, IPanelsController
{
    public ISettingsService SettingsService { get; }
    public IEditService EditService { get; }
    public IToolService ToolService { get; }
    public IPlatformStuffService PlatformStuffService { get; }
    public IAppState AppState { get; }
    public IViewModelService ViewModelService { get; }
    public Action OpenWorkSpaceCallback { get; set; }

    public bool IsBusy => AppState.IsBusy;

    public event EventHandler MenuClosed;
    public event EventHandler SidebarModeChanged;
    public event EventHandler<bool> LayerPropertiesVisibilityChanged;

    public bool ShowMenu
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                if (!value)
                {
                    OnMenuClosed();
                }
                else
                {
                    ViewModelService.GetViewModel<MainMenuViewModel>()?.OnMenuShown();
                }
            }
        }
    }

    public bool ShowToolProperties
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                if (value)
                {
                    ShowBrushSettings = false;
                    if (ShowColorEditor && !PinColorPicker) ToggleColorEditorCommand.Execute(null);
                }
            }
        }
    }


    public ToolBarViewModel ToolBarViewModel => ViewModelService.GetViewModel<ToolBarViewModel>();

    public bool ShowSidebar
    {
        get => Get<bool>();
        set
        {
            if (Set(value)) SetSettingAsync("SidebarVisibilityState", value);
        }
    }

    public bool ShowLayerProperties
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                OnLayerPropertiesShown(value);
            }
        }
    }

    public bool ShowColorEditor
    {
        get => Get<bool>();
        set => Set(value);
    }
    public bool PinColorPicker
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowExtraTools
    {
        get => Get<bool>();
        set => Set(value);
    }
    public bool ShowClipboardBar
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowAssetsLibrary
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowExportDialog
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                ViewModelService.GetViewModel<ExportPageViewModel>()?.Load();
            }
        }
    }

    public bool ShowTimeline
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                GetService<ISettingsService>().Set("TimelineVisibilityState", value);
            }
        }
    }

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool SpriteEditMode => CurrentEditContext == EditContextType.Sprite;

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool IsDefaultEditContextSelected => CurrentEditContext == AppState.CurrentProject.DefaultEditContextType;

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool ShowSceneTree
    {
        get => Get<bool>() && CurrentEditContext == EditContextType.General;
        set
        {
            if (Set(value)) SetSettingAsync("SceneTreeVisibilityState", value);
        }
    }

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool ShowSceneTreeButton => CurrentEditContext == EditContextType.General;

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool ShowLayersButton => CurrentEditContext == EditContextType.Sprite;

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool ShowLayers
    {
        get => Get<bool>() && CurrentEditContext == EditContextType.Sprite;
        set
        {
            if (Set(value)) SetSettingAsync("LayersListVisibilityState", value);
        }
    }

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool CanShowLayers => CurrentEditContext == EditContextType.Sprite;


    public EditContextType CurrentEditContext => AppState.CurrentProject.CurrentContextType;

    public bool ShowCanvasResizePanel
    {
        get => Get<bool>();
        set => Set(value);
    }

    public void OpenWorkSpace()
    {
        OpenWorkSpaceCallback?.Invoke();
    }

    public bool ShowBrushSettings
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                if (value)
                {
                    ShowToolProperties = false;
                    if (ShowColorEditor && !PinColorPicker) ToggleColorEditorCommand.Execute(null);
                }
            }
        }
    }

    public int GridCellSizeWidth
    {
        get => (int) CoreServices.SnappingService.GridCellSize.Width;
        set
        {
            var oldSize = CoreServices.SnappingService.GridCellSize;
            CoreServices.SnappingService.GridCellSize = new SKSize(value, oldSize.Height);
            OnPropertyChanged();
        }
    }
    public int GridCellSizeHeight
    {
        get => (int)CoreServices.SnappingService.GridCellSize.Height;
        set
        {
            var oldSize = CoreServices.SnappingService.GridCellSize;
            CoreServices.SnappingService.GridCellSize = new SKSize(oldSize.Width, value);
            OnPropertyChanged();
        }
    }

    public bool ShowGrid
    {
        get => CoreServices.SnappingService.ShowGrid;
        set
        {
            CoreServices.SnappingService.ShowGrid = value;
            OnPropertyChanged();
        }
    }

    public bool ShowPreviewPanel
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                GetService<ISettingsService>().Set("PreviewPanelVisibilityState", value);
            }
        }
    }

    public bool ShowTextBar
    {
        get => Get(false);
        set => Set(value);
    }

    public bool MirrorX
    {
        get => Get<bool>(false);
        set
        {
            if (Set(value))
                CoreServices.DrawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Horizontal, value);
        }
    }

    public bool MirrorY
    {
        get => Get<bool>(false);
        set
        {
            if (Set(value))
                CoreServices.DrawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Vertical, value);
        }
    }

    public bool LockAxis
    {
        get => Get<bool>(false);
        set
        {
            if (Set(value))
                CoreServices.SnappingService.ForceAspectLock = value;
        }
    }


    public bool ShowRatePrompt
    {
        get => Get<bool>(false);
        set => Set(value);
    }

    public string WindowTitle
    {
        get => Get("Hlello");
        set => Set(value);
    }

    public ReviewHelpers ReviewHelper { get; set; }
    public string RatePromptMessage => ReviewHelper.RatePromptMessage;
    public string RatePromptButtonText => ReviewHelper.RatePromptButtonText;

    public IRelayCommand ChangeTitleCommand => GetCommand(() => WindowTitle = "GGGGGG!!!!");

    //see EditContextView.xaml
    public IRelayCommand ApplyEditCommand => GetCommand(() => EditService.ApplyCurrentEdit());
    public IRelayCommand ToggleColorEditorCommand => GetCommand(() =>
    {
        ShowColorEditor = !ShowColorEditor;

        if (ShowColorEditor && !PinColorPicker)
        {
            ShowBrushSettings = false;
            ShowToolProperties = false;
        }

        if (!ShowColorEditor && AppState.CurrentProject.CurrentTool is EyedropperTool)
        {
            var cpvm = ViewModelService.GetViewModel<ColorPickerViewModel>();
            cpvm.ActivateEyeDropperCommand.Execute(null);
        }
    });

    public IRelayCommand ToggleBrushSettingsCommand => GetCommand(() =>
    {
        ShowBrushSettings = !ShowBrushSettings;
    });

    public IRelayCommand ToggleSidebarModeCommand => GetCommand(() =>
    {
        ShowSidebar = !ShowSidebar;
        OnSidebarModeChanged();
    });
    public IRelayCommand ToggleSceneTreeCommand => GetCommand(() =>
    {
        ShowSceneTree = !ShowSceneTree;
    });
    public IRelayCommand TogglePreviewPanelCommand => GetCommand(() =>
    {
        ShowPreviewPanel = !ShowPreviewPanel;
    });
    public IRelayCommand ToggleCanavsSizePanelCommand => GetCommand(() =>
    {
        ShowCanvasResizePanel = !ShowCanvasResizePanel;
    });

    public ICommand RateAppCommand => GetCommand(async () =>
    {
        var result = await CoreServices.LicenseService.RateApp();
        if (result)
        {
            ShowRatePrompt = false;

            SettingsService.Set("IsAppReviewed", true);
            ReviewHelper.LogReview("Updated");
        }
        else
        {
            ReviewHelper.LogReview("Not updated");
        }
    });
    public ICommand CloseRatePromptCommand => GetCommand(() =>
    {
        ShowRatePrompt = false;
        ReviewHelper.LogReview("Prompt closed");
        ReviewHelper.DefferNextReviewPrompt();
    });

    public ICommand HideExportDialogCommand => GetCommand(() => ShowExportDialog = false);
    public ICommand HideMenuCommand => GetCommand(() => ShowMenu = false);

    public ICommand RotateCommand => MapCommand(SpritePlugin.EditCommands.Rotate90);
    public ICommand FlipHorizontalCommand => MapCommand(SpritePlugin.EditCommands.FlipHorizontal);
    public ICommand FlipVerticalCommand => MapCommand(SpritePlugin.EditCommands.FlipVertical);
    public ICommand ToggleGridCommand => MapCommand(Commands.View.Snapping.ToggleGrid, () =>
    {
        OnPropertyChanged(nameof(ShowGrid));
    });

    public ICommand ImportCommand => MapCommand(Commands.Edit.Import);

    public ICommand PasteCommand => MapCommand(SpritePlugin.EditCommands.TryPaste);
    public ICommand CopyCommand => MapCommand(SpritePlugin.EditCommands.CopyPixels);
    public ICommand CutCommand => MapCommand(SpritePlugin.EditCommands.CutPixels);
    public ICommand CropCommand => MapCommand(SpritePlugin.EditCommands.CropPixels);
    public ICommand FillSelectionCommand => MapCommand(SpritePlugin.EditCommands.FillSelectionCommand);
    public ICommand SelectObjectCommand => MapCommand(SpritePlugin.EditCommands.SelectObjectCommand);


    public MainViewModel(SimpleContainer container, ISettingsService settingsService, IEditService editService,
        IToolService toolService, IPlatformStuffService platformStuffService, IAppState appState, IMessenger messenger,
        IViewModelService viewModelService)
    {
        container.RegisterInstance<IMenuController>(this);
        container.RegisterInstance<IPanelsController>(this);

        ShowMenu = false;
        SettingsService = settingsService;
        EditService = editService;
        ToolService = toolService;
        PlatformStuffService = platformStuffService;
        AppState = appState;
        ViewModelService = viewModelService;

        AppState.CurrentProject.WatchFor(x => x.CurrentContextType, () => OnPropertyChanged(nameof(CurrentEditContext)));
        AppState.WatchFor(s => s.IsBusy, () => OnPropertyChanged(nameof(IsBusy)));
        AppState.CurrentProject.WatchFor(s => s.CurrentContextType, () => OnPropertyChanged(nameof(CurrentEditContext)));

        ReviewHelper = new ReviewHelpers(settingsService);

        if (IsDesignMode || EditService == null)
            return;

        messenger.Register<ProjectSavedMessage>(this, m => TrySuggestRate("Save"));
        messenger.Register<ProjectLoadedMessage>(this,
            m => ShowTimeline = EditService.CurrentEditedNode is Pix2dSprite sprite && sprite.GetFramesCount() > 1);

        InvokeWithoutOnPropertyChanged(() =>
        {
            ShowLayers = SettingsService.Get<bool>("LayersListVisibilityState");
            ShowSidebar = SettingsService.Get<bool>("SidebarVisibilityState");
            ShowTimeline = SettingsService.Get<bool>("TimelineVisibilityState");
            ShowSceneTree = SettingsService.Get<bool>("SceneTreeVisibilityState");
            ShowPreviewPanel = SettingsService.Get<bool>("PreviewPanelVisibilityState");

            ReviewHelper.InitRatePromptMessage();
        });
    }

    public void TrySuggestRate(string contextTitle)
    {
        ShowRatePrompt = ReviewHelper.TrySuggestRate(contextTitle);
        OnPropertyChanged(nameof(RatePromptMessage));
        OnPropertyChanged(nameof(RatePromptButtonText));
    }
    
    private Task SetSettingAsync<T>(string key, T value)
    {
        SettingsService?.Set<T>(key, value);
        return Task.CompletedTask;
    }

    protected virtual void OnMenuClosed()
    {
        MenuClosed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnLayerPropertiesShown(bool isVisible)
    {
        LayerPropertiesVisibilityChanged?.Invoke(this, isVisible);
    }

    protected virtual void OnSidebarModeChanged()
    {
        SidebarModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetVisualState(string stateName)
    {
        if (stateName == "NarrowState")
        {
            ShowSidebar = false;
            ShowSceneTree = false;
            OnSidebarModeChanged();
        }
    }

    public void OnCanvasClicked()
    {
        if (ShowColorEditor && !PinColorPicker && !(AppState.CurrentProject.CurrentTool is EyedropperTool))
            ToggleColorEditorCommand.Execute(null);

        ShowToolProperties = false;
        ShowBrushSettings = false;
    }

}
