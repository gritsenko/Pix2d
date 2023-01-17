using System.Collections.Generic;
using Avalonia.Controls.Shapes;
using Avalonia.Metadata;
using Pix2d.ViewModels.ToolBar;

namespace Pix2d.Common;

public class ToolIconTemplateSelector : IDataTemplate
{
    public bool SupportsRecycling => false;
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = new();

    public IControl Build(object data)
    {
        var toolVm = ((ToolItemViewModel)data);
        var templateKey = toolVm.ToolKey;

        if (toolVm.ToolIconPath != default)
        {
            return new FuncDataTemplate<ToolItemViewModel>((o, ns) =>
                    new Path()
                        .Data(Geometry.Parse(toolVm.ToolIconPath))
                        .Stretch(Stretch.Uniform)
                        .Width(26).Height(26).Fill(Brushes.White))
                .Build(data);
        }

        if (Templates.TryGetValue(templateKey, out var toolTemplate))
        {
            return toolTemplate.Build(data);
        }

        return null;
    }

    public bool Match(object data)
    {
        return data is ToolItemViewModel;
    }
}