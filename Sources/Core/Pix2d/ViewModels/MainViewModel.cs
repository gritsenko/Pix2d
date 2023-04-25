using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.State;
using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.ViewModels.Export;
using Pix2d.ViewModels.MainMenu;

namespace Pix2d.ViewModels;

public class MainViewModel : Pix2dViewModelBase, IMenuController, IPanelsController
{
    public ISettingsService SettingsService { get; }
    public IAppState AppState { get; }
    public IViewModelService ViewModelService { get; }

    public bool IsBusy => AppState.IsBusy;

    public event EventHandler<bool> LayerPropertiesVisibilityChanged;

    public bool ShowMenu
    {
        get => Get<bool>();
        set
        {
            if (Set(value))
            {
                if (value)
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
            if (Set(value) && value)
            {
                ShowBrushSettings = false;
                if (ShowColorEditor && !PinColorPicker) ToggleColorEditorCommand.Execute(null);
            }
        }
    }

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

    [NotifiesOn(nameof(CurrentEditContext))]
    public bool ShowTimeline
    {
        get => Get<bool>() && CurrentEditContext == EditContextType.Sprite;
        set
        {
            if (Set(value)) SetSettingAsync("TimelineVisibilityState", value);
        }
    }

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
                SetSettingAsync("PreviewPanelVisibilityState", value);
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

    public ReviewHelpers ReviewHelper { get; set; }
    public string RatePromptMessage => ReviewHelper.RatePromptMessage;
    public string RatePromptButtonText => ReviewHelper.RatePromptButtonText;
    public IRelayCommand ToggleColorEditorCommand => GetCommand(() =>
    {
        ShowColorEditor = !ShowColorEditor;

        if (ShowColorEditor && !PinColorPicker)
        {
            ShowBrushSettings = false;
            ShowToolProperties = false;
        }
    });

    public IRelayCommand ToggleBrushSettingsCommand => GetCommand(() =>
    {
        ShowBrushSettings = !ShowBrushSettings;
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

            SetSettingAsync("IsAppReviewed", true);
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
    public ICommand ToggleGridCommand => MapCommand(Commands.View.Snapping.ToggleGrid, () =>
    {
        OnPropertyChanged(nameof(ShowGrid));
    });

    public MainViewModel(SimpleContainer container, ISettingsService settingsService, IAppState appState, IMessenger messenger, IViewModelService viewModelService)
    {
        container.RegisterInstance<IMenuController>(this);
        container.RegisterInstance<IPanelsController>(this);

        ShowMenu = false;
        SettingsService = settingsService;
        AppState = appState;
        ViewModelService = viewModelService;

        AppState.WatchFor(s => s.IsBusy, () => OnPropertyChanged(nameof(IsBusy)));
        AppState.CurrentProject.WatchFor(s => s.CurrentContextType, () => OnPropertyChanged(nameof(CurrentEditContext)));

        ReviewHelper = new ReviewHelpers(settingsService);

        if (IsDesignMode)
            return;

        messenger.Register<ProjectSavedMessage>(this, m => TrySuggestRate("Save"));

        InvokeWithoutOnPropertyChanged(() =>
        {
            ShowLayers = SettingsService.Get<bool>("LayersListVisibilityState");
            ShowSidebar = SettingsService.Get<bool>("SidebarVisibilityState");
            ShowTimeline = SettingsService.Get<bool>("TimelineVisibilityState");
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

    protected virtual void OnLayerPropertiesShown(bool isVisible)
    {
        LayerPropertiesVisibilityChanged?.Invoke(this, isVisible);
    }
}