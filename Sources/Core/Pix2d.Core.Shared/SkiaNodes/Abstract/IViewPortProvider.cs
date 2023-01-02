using System;

namespace SkiaNodes
{
    public interface IViewPortProvider
    {
        event EventHandler ViewPortInitialized;
        ViewPort ViewPort { get; }
        void Refresh();
    }
}