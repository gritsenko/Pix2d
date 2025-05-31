namespace Pix2d.Abstract.Tools;

public interface IToolService
{
    void RegisterTool<TTool>(EditContextType contextType) where TTool : ITool;
    void ActivateTool(string key);
    void ActivateTool<TTool>();
}