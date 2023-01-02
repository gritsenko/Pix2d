using System;

namespace Pix2d.Abstract.UI
{
    public interface IMenuController
    {
        event EventHandler MenuClosed;
        event EventHandler SidebarModeChanged;
        
        bool ShowMenu { get; set; }
        
        bool ShowToolProperties { get; set; }
        
        bool ShowBrushSettings { get; set; }

        bool ShowExtraTools { get; set; }
        bool ShowTimeline { get; set; }
        bool ShowAssetsLibrary { get; set; }

        bool ShowExportDialog { get; set; }
        bool ShowClipboardBar { get; set; }
        bool ShowSidebar { get; set; }
        bool ShowSceneTree { get; set; }
        bool ShowPreviewPanel { get; set; }

        bool ShowTextBar { get; set; }

        void TrySuggestRate(string contextTitle);

    }
}