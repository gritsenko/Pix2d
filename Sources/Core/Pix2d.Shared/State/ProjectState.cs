#nullable enable
using Pix2d.Abstract;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Selection;
using Pix2d.Abstract.State;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.State;

public class ProjectState : StateBase
{
    public string? Title => string.IsNullOrWhiteSpace(FileName) ? "New project" : FileName.Replace(".pix2d", "");
    public string? FileName => File?.Title;
    public bool HasUnsavedChanges { get; set; }
    public bool IsNewProject => File == null;

    public IFileContentSource? File
    {
        get => Get<IFileContentSource>();
        set => Set(value);
    }

    public SKNode? SceneNode { get; set; }

    public SKNode? CurrentEditedNode
    {
        get => Get<SKNode>();
        set => Set(value);
    }

    public virtual INodeEditor? CurrentNodeEditor
    {
        get => Get<INodeEditor>();
        set => Set(value);
    }

    public SKNode? FrameEditorNode { get; set; }

    public INodesSelection Selection { get; set; }

    public SKSize SelectionSize => HasSelection ? Selection.Bounds.Size : (SceneNode?.GetChildrenBounds().Size ?? SKSize.Empty);
    public bool HasSelection => Selection?.Nodes?.Any() == true;
    public EditContextType DefaultEditContextType { get; set; } = EditContextType.Sprite;
    public EditContextType CurrentContextType
    {
        get => Get(EditContextType.Sprite);
        set => Set(value);
    }

    public ViewPortState ViewPortState { get; set; } = new();
    #region Not serializable

    public SessionInfo LastSessionInfo { get; set; }
    //public bool IsAnimationPlaying { 
    //    get => Get<bool>();
    //    set => Set(value);
    //}

    #endregion
}