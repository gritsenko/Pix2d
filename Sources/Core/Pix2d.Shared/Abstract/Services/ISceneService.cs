using System.Collections.Generic;
using SkiaNodes;
using SkiaNodes.Abstract;

namespace Pix2d.Abstract.Services
{
    public interface ISceneService
    {
        SKNode GetRootNode();
        SKNode GetCurrentScene();
        IList<TContainer> GetCurrentSceneContainers<TContainer>()
            where TContainer : IContainerNode;
    }
}