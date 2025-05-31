#nullable enable
using System.Reflection;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Resources;
using Pix2d.Tools;

namespace Pix2d.Services;

public class ToolService : IToolService
{
    private readonly IMessenger _messenger;
    private readonly AppState _appState;
    private readonly Func<Type, object> _activatorFactoryFunc;
    private ToolsState ToolsState => _appState.ToolsState;

    private readonly Dictionary<EditContextType, string> _defaultContextTool = new();

    public ToolService(IMessenger messenger, AppState appState, Func<Type, object> activatorFactoryFunc)
    {
        _messenger = messenger;
        _appState = appState;
        _activatorFactoryFunc = activatorFactoryFunc;
        messenger.Register<ProjectLoadedMessage>(this, _ => ActivateDefaultTool());
        _appState.CurrentProject.WatchFor(x => x.CurrentContextType, ActivateDefaultTool);
        _appState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, OnAnimationStateChanged);
        RegisterTool<ObjectManipulationTool>(EditContextType.General);
    }

    public void ActivateTool(string key)
    {
        var tool = GetToolStateByKey(key);

        if (tool == null || ToolsState.CurrentToolKey == key) return;

        tool.ToolInstance ??= (ITool)_activatorFactoryFunc(tool.ToolType);

        var oldTool = GetToolStateByKey(ToolsState.CurrentToolKey);
        oldTool?.ToolInstance?.Deactivate();
        ToolsState.CurrentToolKey = tool.Name;
        tool.ToolInstance?.Activate();

        if (tool.ToolInstance != null)
            _messenger.Send(new CurrentToolChangedMessage(tool.ToolInstance));
    }

    public void ActivateTool<TTool>()
    {
        ActivateTool(typeof(TTool).Name);
    }

    private ToolState? GetToolStateByKey(string key) => ToolsState.Tools.FirstOrDefault(x => x.Name == key);

    public void ActivateDefaultTool()
    {
        var context = _appState.CurrentProject.CurrentContextType;
        if (!_defaultContextTool.TryGetValue(context, out var defaultTool))
        {
            defaultTool = _defaultContextTool[EditContextType.General];
        }

        ActivateTool(defaultTool);
    }

    private void OnAnimationStateChanged()
    {
        var tool = GetToolStateByKey(ToolsState.CurrentToolKey);

        if (!_appState.CurrentProject.IsAnimationPlaying || tool?.EnabledDuringAnimation == true)
            return;

        ActivateDefaultTool();
    }

    public void RegisterTool<TTool>(EditContextType context)
        where TTool : ITool
    {
        var toolType = typeof(TTool);
        var toolState = new ToolState()
        {
            ToolType = toolType,
            Context = context
        };
        _defaultContextTool.TryAdd(context, toolState.Name);

        var toolAttr = toolType.GetCustomAttribute<Pix2dToolAttribute>();
        if (toolAttr != null)
        {
            toolState.HasToolProperties = toolAttr.HasSettings;
            toolState.EnabledDuringAnimation = toolAttr.EnabledDuringAnimation;
            toolState.ToolTip = toolAttr.HotKey != null ? $"{toolAttr.DisplayName} ({toolAttr.HotKey})" : toolAttr.DisplayName;
            toolState.GroupName = toolAttr.Group ?? "";
            toolState.IconKey = toolType.Name;
            toolState.IconData = toolAttr.IconData;
            if (toolAttr.SettingsViewType != null)
                toolState.TopBarUi = () => _activatorFactoryFunc.Invoke(toolAttr.SettingsViewType);
        }


        _appState.ToolsState.Tools.Add(toolState);

        if (toolState.IconData != null && !ToolIcons.ToolIconTemplateSelector.Templates.ContainsKey(toolState.Name))
        {
            ToolIcons.ToolIconTemplateSelector.Templates.Add(toolState.Name, ToolIcons.GetToolTemplate(toolState.IconData));
        }
    }
}