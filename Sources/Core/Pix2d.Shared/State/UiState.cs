using Pix2d.Abstract.State;

namespace Pix2d.State;

public class UiState : StateBase
{
    public bool ShowMenu { get; set; }

    public bool ShowToolProperties { get; set; }

    public bool ShowBrushSettings { get; set; }

    public bool ShowExtraTools { get; set; }
    public bool ShowTimeline { get; set; }
    public bool ShowAssetsLibrary { get; set; }

    public bool ShowExportDialog { get; set; }
    public bool ShowClipboardBar { get; set; }
    public bool ShowSidebar { get; set; }
    public bool ShowSceneTree { get; set; }
    public bool ShowPreviewPanel { get; set; }

    public bool ShowTextBar { get; set; }
}