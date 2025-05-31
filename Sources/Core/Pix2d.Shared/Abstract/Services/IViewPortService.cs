using SkiaNodes;
using SkiaNodes.Abstract;

namespace Pix2d.Abstract.Services;

public interface IViewPortService : IViewPortProvider
{
    void ShowAll();
    void Initialize(ViewPort viewPort);
}