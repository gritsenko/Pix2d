using System;
using Pix2d.Abstract.Tools;
using Pix2d.Mvvm;
using Pix2d.ViewModels.ToolSettings;

namespace Pix2d.ViewModels.ToolBar;

public class ToolItemViewModel : Pix2dViewModelBase
{
    private readonly Func<ToolSettingsBaseViewModel> _settingsVmProvider;
    public string ToolKey { get; set; }

    public string ToolIconPath { get; } = null!;
    public bool IsSelected
    {
        get => Get<bool>();
        set => Set(value);
    }

    public string Title { get; }

    public string Tooltip { get; set; }
    public bool HasToolProperties => _settingsVmProvider != null;

    public ToolItemViewModel(string toolKey, Func<ToolSettingsBaseViewModel> settingsVmProvider)
    {
        _settingsVmProvider = settingsVmProvider;
        ToolKey = toolKey;
            
        var tool = CoreServices.ToolService.GetToolByKey(toolKey);
        Title = tool.DisplayName.ToUpper();

        if (tool is BaseTool baseTool)
            ToolIconPath = baseTool.ToolIconData;

        Tooltip = tool.HotKey != null ? $"{tool.DisplayName} ({tool.HotKey})" : tool.DisplayName;
    }

    public ToolSettingsBaseViewModel GetSettingsVm()
    {
        return _settingsVmProvider?.Invoke();
    }

    public void InvalidateIsSelected()
    {
        OnPropertyChanged(nameof(IsSelected));
    }
}