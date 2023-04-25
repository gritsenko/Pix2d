namespace Pix2d.Abstract.UI;

public interface IMenuController
{
    bool ShowMenu { get; set; }
        
    bool ShowToolProperties { get; set; }
        
    bool ShowExtraTools { get; set; }
    bool ShowTimeline { get; set; }

    bool ShowExportDialog { get; set; }
    bool ShowClipboardBar { get; set; }
    bool ShowSidebar { get; set; }
    bool ShowPreviewPanel { get; set; }

    bool ShowTextBar { get; set; }

    void TrySuggestRate(string contextTitle);

}