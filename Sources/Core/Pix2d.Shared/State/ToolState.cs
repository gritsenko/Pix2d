#nullable enable
using Pix2d.Abstract;
using Pix2d.Abstract.Tools;
using System;

namespace Pix2d.State;

public class ToolState
{
    public string? GroupName { get; set; }

    public Func<object>? TopBarUi { get; set; }

    public bool EnabledDuringAnimation { get; set; }

    public EditContextType Context { get; set; }
    public string Name => ToolType.Name;
    public Type ToolType { get; set; } = null!;

    public ITool? ToolInstance { get; set; }

    public string? IconKey { get; set; }

    public string? ToolTip { get; set; }

    public bool HasToolProperties { get; set; }
    public string? IconData { get; set; }
}