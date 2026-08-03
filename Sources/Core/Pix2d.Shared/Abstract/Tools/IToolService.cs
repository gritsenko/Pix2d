namespace Pix2d.Abstract.Tools;

public interface IToolService
{
    void RegisterTool<TTool>(EditContextType contextType) where TTool : ITool;
    void ActivateTool(Type toolType);
    void ActivateTool<TTool>();

    /// <summary>
    /// Name of the tool that is about to become active. Non-null only inside the brief window between an
    /// outgoing tool's <c>Deactivate</c> and the incoming tool's <c>Activate</c> — lets outgoing tools make
    /// transition-aware decisions (e.g. keep the marquee when handing off to the transform tool).
    /// </summary>
    string? IncomingToolKey { get; }

    /// <summary>
    /// Switches back to the tool that was active before the current one. No-op (returns false) when there is
    /// no previous tool, it is the current one, or it belongs to another edit context.
    /// </summary>
    bool ActivatePreviousTool();

    /// <summary>
    /// True when <paramref name="toolKey"/> identifies a registered tool implementing
    /// <see cref="IPixelSelectionTool"/>. Lets the transform tool decide whether to keep the marquee alive
    /// during a hand-off without hard-coding the set of selection tools — new selection plugins (AI,
    /// future tools) participate automatically.
    /// </summary>
    bool IsSelectionTool(string? toolKey);
}