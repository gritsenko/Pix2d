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
}