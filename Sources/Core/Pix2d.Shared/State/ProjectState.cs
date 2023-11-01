using System;
using System.Linq;
using Pix2d.Abstract;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Selection;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.State;

public class ProjectState : StateBase
{
    public string Title => string.IsNullOrWhiteSpace(FileName) ? "New project" : FileName.Replace(".pix2d", "");
    public string FileName => File?.Title;
    public bool HasUnsavedChanges { get; set; }
    public bool IsNewProject => File == null;
    public bool IsModified { get; set; }
    public IFileContentSource File { get => Get<IFileContentSource>(); set => Set(value); }
    public SKNode SceneNode { get; set; }
    public SKNode CurrentEditedNode { get; set; }
    public SKNode FrameEditorNode { get; set; }

    public INodesSelection Selection { get; set; }

    public SKSize SelectionSize => HasSelection ? Selection.Bounds.Size : (SceneNode?.GetChildrenBounds().Size ?? SKSize.Empty);
    public bool HasSelection => Selection?.Nodes?.Any() == true;

    [Obsolete]
    //вот молодец! написал что обсолете, а на что менять не написал! мда!
    public ITool CurrentTool { get; set; }
    public INodeEditor CurrentNodeEditor { get; set; }
    public EditContextType DefaultEditContextType { get; set; }
    public EditContextType CurrentContextType
    {
        get => Get(EditContextType.General);
        set => Set(value);
    }

    public ViewPortState ViewPortState { get; set; } = new();
    #region Not serializable

    public bool IsProcessing { get; set; }
    public SessionInfo LastSessionInfo { get; set; }
    public bool IsAnimationPlaying { 
        get => Get<bool>();
        set => Set(value);
    }

    #endregion
}