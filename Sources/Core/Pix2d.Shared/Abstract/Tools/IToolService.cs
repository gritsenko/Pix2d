namespace Pix2d.Abstract.Tools;

public interface IToolService
{
    void RegisterTool<TTool>(EditContextType contextType) where TTool : ITool;
    void ActivateTool(Type toolType);
    void ActivateTool<TTool>();
}