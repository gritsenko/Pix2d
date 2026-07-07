using Newtonsoft.Json;

namespace SkiaNodes;

public class NodeDesignerState
{
    // Editor selection is transient UI state — must not persist (a saved file should not reopen with
    // a stale "selected" node).
    [JsonIgnore]
    public bool IsSelected { get; set; }

    public bool IsLocked { get; set; }

    public bool? LockAspect { get; set; }

    public bool? ShowChildrenInTree { get; set; } = true;

    public NodeExportSettings ExportSettings { get; set; } = new NodeExportSettings();

    // Tree-panel expansion is transient UI state.
    [JsonIgnore]
    public bool IsExpanded { get; set; }
}

public class NodeExportSettings
{
    public NodeExportMode ExportMode { get; set; }
    public string TextureKey { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string OnClickHandlerName { get; set; } = string.Empty;
    public float ExportScale { get; set; } = 1;
    public NodeExportFormat ExportFormat { get; set; } = NodeExportFormat.Png;
}

public enum NodeExportFormat
{
    Png,
    Jpg
}
public enum NodeExportMode
{
    Export,
    Ignore
}