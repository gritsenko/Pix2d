using Pix2d.Abstract.State;

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

}