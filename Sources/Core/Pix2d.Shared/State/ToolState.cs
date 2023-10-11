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
        }

        if (toolType.GetProperty("ToolSettings")?.GetValue(null) is ToolSettings settings)
        {
            ToolTip = settings.HotKey != null ? $"{settings.DisplayName} ({settings.HotKey})" : settings.DisplayName;
            IconKey = settings.IconData;
            TopBarUI = settings.TopBarUI;
        }
    }

    public Func<object>? TopBarUI { get; set; }

    public bool EnabledDuringAnimation { get; set; }

    public EditContextType Context { get; set; }
    public string Name { get; set; }
    public Type ToolType { get; set; }

    public ITool? ToolInstance { get; set; }

    public string? IconKey { get; set; }

    public string ToolTip { get; }

    public bool HasToolProperties { get; }
}