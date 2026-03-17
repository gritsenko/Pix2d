using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pix2d.UI.Common;

public static class ExperimentalExtensions
{
    public static TPanel _Children<TPanel>(this TPanel container, List<Control> children)
        where TPanel : Panel
    {
        foreach (var child in children)
            container.Children.Add(child);
        return container;
    }

}