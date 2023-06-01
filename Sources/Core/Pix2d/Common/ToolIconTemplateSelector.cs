using System;
using System.Collections.Generic;
using Avalonia.Controls.Shapes;
using Avalonia.Metadata;

namespace Pix2d.Common;

public class ToolIconTemplateSelector : IDataTemplate
{
    public bool SupportsRecycling => false;
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = new();

    public Control Build(object data)
    {
        var toolType = (Type)data;
        var templateKey = toolType.Name;

        //if (toolType.ToolIconPath != default)
        //{
        //    return new FuncDataTemplate<ToolItemViewModel>((o, ns) =>
        //            new Path()
        //                .Data(Geometry.Parse(toolType.ToolIconPath))
        //                .Stretch(Stretch.Uniform)
        //                .Width(26).Height(26).Fill(Brushes.White))
        //        .Build(data);
        //}

        if (Templates.TryGetValue(templateKey, out var toolTemplate))
        {
            return toolTemplate.Build(data);
        }

        return null;
    }

    public bool Match(object data)
    {
        return data is Type;
    }
}