using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

public static class SelectionToolActivationExtensions
{
    /// <summary>
    /// Activates the marquee tool the user came from before entering <see cref="PixelTransformTool"/>
    /// (<see cref="SelectionState.ReturnSelectionToolKey"/>), falling back to
    /// <see cref="PixelSelectRectTool"/> when that key no longer names a usable selection tool. Shared by
    /// every affordance that ends a transform session from outside the tool — Esc, Apply, Deselect — because
    /// the hand-off is also what commits the lifted pixels (see <c>PixelTransformTool.Deactivate</c>).
    /// </summary>
    public static void ActivateReturnSelectionTool(this IToolService toolService, AppState appState)
    {
        var toolKey = appState.SelectionState.ReturnSelectionToolKey;
        if (!toolService.IsSelectionTool(toolKey) || toolKey == nameof(PixelTransformTool))
            toolKey = nameof(PixelSelectRectTool);

        var toolType = appState.ToolsState.Tools.FirstOrDefault(x => x.Name == toolKey)?.ToolType;
        if (toolType != null)
            toolService.ActivateTool(toolType);
        else
            toolService.ActivateTool<PixelSelectRectTool>();
    }
}
