#nullable enable
using Pix2d.Abstract;
using Pix2d.Abstract.Tools;
using System;
using System.Reflection;

namespace Pix2d.State;

public class ToolState
{
    public ToolState(Type toolType)
    {
        Name = toolType.Name;
        this.ToolType = toolType;
        var toolAttr = toolType.GetCustomAttribute<Pix2dToolAttribute>();
        if (toolAttr != null)
        {
            HasToolProperties = toolAttr.HasSettings;
            EnabledDuringAnimation = toolAttr.EnabledDuringAnimation;
            ToolTip = toolAttr.HotKey != null ? $"{toolAttr.DisplayName} ({toolAttr.HotKey})" : toolAttr.DisplayName;
            IconKey = toolAttr.IconData;
            GroupName = toolAttr.Group ?? "";
            if (toolAttr.SettingsViewType != null) TopBarUi = () => IoC.Create<object>(toolAttr.SettingsViewType);
        }

        IconKey ??= toolType.Name;
    }

    public string GroupName { get; set; }

    public Func<object>? TopBarUi { get; set; }

    public bool EnabledDuringAnimation { get; set; }

    public EditContextType Context { get; set; }
    public string Name { get; set; }
    public Type ToolType { get; set; }

    public ITool? ToolInstance { get; set; }

    public string? IconKey { get; }

    public string ToolTip { get; } = "";

    public bool HasToolProperties { get; }
}