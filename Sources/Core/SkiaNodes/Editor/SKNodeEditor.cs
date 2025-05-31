namespace SkiaNodes.Editor;

public class SkNodesEditor
{
    private SKNode[] _selectedNodes;


    /// <summary>
    /// Nodes selected for editing
    /// </summary>
    public SKNode[] SelectedNodes
    {
        get => _selectedNodes;
        set => UpdateSelection(value);
    }

    private void UpdateSelection(SKNode[] nodes)
    {
            _selectedNodes = nodes;
        }
}