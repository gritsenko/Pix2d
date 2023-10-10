using System;

namespace Pix2d.Abstract
{
    public interface IViewLocator
    {
        Type GetView(string viewName);

        bool HasViewModelMapping(Type viewType);

        Type GetViewModelMapping(Type viewType);

    }
}
