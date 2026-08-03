namespace Pix2d.State;

public class ToolsState : StateBase
{
    public List<ToolState> Tools { get; set; } = [];

    public ToolState? CurrentTool => Tools.FirstOrDefault(x => x.Name == CurrentToolKey);

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

    /// <summary>
    /// Tool that was active before <see cref="CurrentToolKey"/>. Lets a "one-shot" tool hand control back
    /// where the user came from — currently the eyedropper's auto-return option (#215), which is how a
    /// touch user picks a color without a two-step trip back to the brush.
    /// </summary>
    public string? PreviousToolKey
    {
        get => Get<string?>();
        set => Set(value);
    }

    /// <summary>
    /// True while a brush-family tool is temporarily acting as the eyedropper (Alt held). Drives the
    /// eyedropper cursor and the toolbar highlight so the transient color-pick mode is visible (#184).
    /// </summary>
    public bool IsColorPickerModeActive
    {
        get => Get<bool>();
        set => Set(value);
    }
}