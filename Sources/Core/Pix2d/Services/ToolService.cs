using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Tools;
using Pix2d.UI.Resources;

namespace Pix2d.Services;

public class ToolService : IToolService
{
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    private ToolsState ToolsState => AppState.ToolsState;

    private readonly SimpleContainer _container;

    private readonly Dictionary<EditContextType, string> _defaultContextTool = new();

    public ToolService(SimpleContainer container, IMessenger messenger, AppState appState)
    {
        Messenger = messenger;
        AppState = appState;
        _container = container;
        messenger.Register<ProjectLoadedMessage>(this, m => ActivateDefaultTool());
    }

    public void ActivateTool(string key)
    {
        var tool = GetToolStateByKey(key);

        if (tool == null || ToolsState.CurrentToolKey == key) return;

        tool.ToolInstance ??= _container.BuildInstance(tool.ToolType) as ITool;

        var oldTool = GetToolStateByKey(ToolsState.CurrentToolKey);
        oldTool?.ToolInstance?.Deactivate();
        ToolsState.CurrentToolKey = tool.Name;
        tool.ToolInstance?.Activate();

        Messenger.Send(new CurrentToolChangedMessage(tool.ToolInstance));
    }

    public void ActivateTool<TTool>()
    {
        ActivateTool(typeof(TTool).Name);
    }

    private ToolState? GetToolStateByKey(string key) => ToolsState.Tools.FirstOrDefault(x => x.Name == key);

    public void ActivateDefaultTool()
    {
        var context = AppState.CurrentProject.CurrentContextType;
        if (!_defaultContextTool.TryGetValue(context, out var defaultTool))
        {
            defaultTool = _defaultContextTool[EditContextType.General];
        }

        ActivateTool(defaultTool);
    }

    public void Initialize()
    {
        RegisterTool<ObjectManipulationTool>(EditContextType.General);
        AppState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, OnAnimationStateChanged);
    }

    private void OnAnimationStateChanged()
    {
        var tool = GetToolStateByKey(ToolsState.CurrentToolKey);

        if (!AppState.CurrentProject.IsAnimationPlaying || tool?.EnabledDuringAnimation == true) 
            return;

        ActivateDefaultTool();
    }

    public void RegisterTool<TTool>(EditContextType context)
        where TTool : ITool
    {
        var toolState = new ToolState(typeof(TTool)) { Context = context };
        if (!_defaultContextTool.ContainsKey(context))
        {
            _defaultContextTool[context] = toolState.Name;
        }

        AppState.ToolsState.Tools.Add(toolState);

        if (toolState.IconKey != null && !ToolIcons.ToolIconTemplateSelector.Templates.ContainsKey(toolState.Name))
        {
            ToolIcons.ToolIconTemplateSelector.Templates.Add(toolState.Name, ToolIcons.GetToolTemplate(toolState.IconKey));
        }
    }
}