using System.Collections.Generic;
using Avalonia.Metadata;
using Pix2d.ViewModels.ToolBar;

namespace Pix2d.Common
{
    public class ToolIconTemplateSelector : IDataTemplate
    {
        public bool SupportsRecycling => false;
        [Content]
        public Dictionary<string, IDataTemplate> Templates { get; } = new();

        public IControl Build(object data)
        {
            if (Templates.TryGetValue(((ToolItemViewModel) data).ToolKey, out var toolTemplate))
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

}
