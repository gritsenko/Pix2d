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
        }
        //ToolTip = ToolState.HotKey != null ? $"{tool.DisplayName} ({tool.HotKey})" : tool.DisplayName
    }

    public EditContextType Context { get; set; }
    public string Name { get; set; }
    public Type ToolType { get; set; }

    public ITool? ToolInstance { get; set; }

    public string? IconKey { get; set; }

    public string ToolTip { get; }

    public bool HasToolProperties { get; }
}