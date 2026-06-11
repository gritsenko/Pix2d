#nullable enable
using Pix2d.Abstract;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Selection;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.State;

public class ProjectState : StateBase
{
    /// <summary>
    /// Stable identity of an open project for the lifetime of the process. Keys the per-project
    /// undo history and any per-project caches; not persisted into .pix2d.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

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

    public INodesSelection Selection { get; set; } = null!;

    public SKSize SelectionSize => HasSelection ? Selection.Bounds.Size : GetCanvasSize();
    public bool HasSelection => Selection?.Nodes?.Any() == true;
    public EditContextType DefaultEditContextType { get; set; } = EditContextType.Sprite;
    public EditContextType CurrentContextType
    {
        get => Get(EditContextType.Sprite);
        set => Set(value);
    }

    public ViewPortState ViewPortState { get; set; } = new();

    private SKSize GetCanvasSize()
    {
        if (CurrentEditedNode != null && CurrentEditedNode.Size.Width > 0 && CurrentEditedNode.Size.Height > 0)
            return CurrentEditedNode.Size;

        return SceneNode?.GetChildrenBounds().Size ?? SKSize.Empty;
    }
}