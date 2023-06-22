using System;
using System.Collections.Generic;

namespace Pix2d.Abstract.Tools;

public interface IToolService
{
    void RegisterTool<TTool>(EditContextType contextType) where TTool : ITool;
    void ActivateTool(string key);
    void ActivateDefaultTool();

    void DeactivateTool();
    void Initialize();
    ITool GetToolByKey(string key);
    IEnumerable<Type> GetTools(EditContextType contextType);
}