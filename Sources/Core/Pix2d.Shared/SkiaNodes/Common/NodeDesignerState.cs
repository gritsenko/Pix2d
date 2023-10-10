namespace SkiaNodes
{
    public class NodeDesignerState
    {
        public bool IsSelected { get; set; }
        public bool IsLocked { get; set; }

        public bool? LockAspect { get; set; }

        public bool? ShowChildrenInTree { get; set; } = true;

        public NodeExportSettings ExportSettings { get; set; } = new NodeExportSettings();
        public bool IsExpanded { get; set; }
    }

    public class NodeExportSettings
    {
        public NodeExportMode ExportMode { get; set; }
        public string TextureKey { get; set; }
        public string ClassName { get; set; }
        public string OnClickHandlerName { get; set; }
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
}