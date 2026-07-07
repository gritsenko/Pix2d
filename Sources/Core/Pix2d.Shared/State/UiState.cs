namespace Pix2d.State;

public class UiState : StateBase
{
    public bool ShowMenu
    {
        get => Get<bool>();
        set => Set(value);
    }


    public bool ShowToolProperties
    {
        get => Get<bool>();
        set => Set(value);
    }
    public bool ShowToolGroup
    {
        get => Get<bool>();
        set => Set(value);
    }


    public bool ShowBrushSettings
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowExtraTools
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowTimeline
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
        set => Set(value);
    }

    public string PreferredExportFormat
    {
        get => Get<string>();
        set => Set(value);
    }

    public bool ShowClipboardBar
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowSidebar
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowSceneTree
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowLayers
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowPreviewPanel
    {
        get => Get<bool>();
        set => Set(value);
    }


    public bool ShowTextBar
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowRatePrompt
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowCanvasResizePanel
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowLayerProperties
    {
        get => Get<bool>();
        set => Set(value);
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

    public string VisualState
    {
        get => Get<string>();
        set => Set(value);
    }

    /// <summary>
    /// True while the "recovered your work after an unexpected close" banner should be shown.
    /// Set by the autosave service when a session is restored and the previous shutdown was not
    /// clean; cleared when the user dismisses the banner. Non-blocking — rendered inline in MainView.
    /// </summary>
    public bool ShowRecoveryNotice
    {
        get => Get<bool>();
        set => Set(value);
    }
}