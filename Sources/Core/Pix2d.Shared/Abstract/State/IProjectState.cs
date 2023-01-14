using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Selection;
using Pix2d.Abstract.Tools;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Abstract.State
{
    public interface IProjectState : IStateBase
    {
        string Title { get; }
        string FileName { get; }
        bool IsNewProject { get; }
        bool HasUnsavedChanges { get; }

        bool IsModified { get; }
        IFileContentSource File { get; }

        SKNode SceneNode { get; }
        SKNode CurrentEditedNode { get; }
        
        SKNode FrameEditorNode { get; }

        INodeEditor CurrentNodeEditor { get; }

        INodesSelection Selection { get; }
        bool HasSelection { get; }

        SKSize SelectionSize { get; }

        ITool CurrentTool { get; }

        EditContextType CurrentContextType { get; }

        bool IsProcessing { get; }
        EditContextType DefaultEditContextType { get; }
    }
}