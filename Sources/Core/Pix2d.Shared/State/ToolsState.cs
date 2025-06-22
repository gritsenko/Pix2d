using Pix2d.Abstract.State;

namespace Pix2d.State;

public class ToolsState : StateBase
{
    public List<ToolState> Tools { get; set; } = [];

    public ToolState CurrentTool => Tools.FirstOrDefault(x => x.Name == CurrentToolKey);

    public string CurrentToolKey
    {
        get => Get<string>();
        set => Set(value);
    }

    public string ActiveToolGroup
    {
        get => Get<string>();
        set => Set(value);
    }

}