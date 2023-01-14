using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Primitives.Edit;
using Pix2d.Services.Edit;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services
{
    public class ObjectCreationService: IObjectCreationService
    {
        public ISelectionService SelectionService { get; }
        public ISceneService SceneService { get; }

        public ObjectCreationService(ISelectionService selectionService, ISceneService sceneService)
        {
            SelectionService = selectionService;
            SceneService = sceneService;
        }

        public void CreateArtboard(SKRect destRect)
        {
            var scene = SceneService.GetCurrentScene();
            var canvas = new ArtboardNode() { Position = destRect.Location.Floor(), Size = destRect.Size.Floor(), Name = "New Artboard" };
            scene.Nodes.Add(canvas);
        }

        public void CreateText(SKRect destRect)
        {
            var scene = SceneService.GetCurrentScene();
            var textNode = new TextNode() { Position = destRect.Location.Floor(), Size = destRect.Size.Floor(), Name = "New text" , Text = "Hello world!"};
            scene.Nodes.Add(textNode);

            SelectionService.Select(textNode);
            SelectionService.Selection.UpdateParents(NodeReparentMode.Overflow);
        }

        public void CreateRectangle(SKRect destRect)
        {
            var scene = SceneService.GetCurrentScene();
            var rectangleNode = new RectangleNode() { Position = destRect.Location.Floor(), Size = destRect.Size.Floor(), Name = "New rectangle" };
            scene.Nodes.Add(rectangleNode);

            SelectionService.Select(rectangleNode);
            SelectionService.Selection.UpdateParents(NodeReparentMode.Overflow);
        }

        public SKNode CreateSprite(SKBitmap bitmap, SKNode groupIntoNode = null)
        {
            var sprite = Pix2dSprite.CreateFromBitmap(bitmap);

            var targetParent = SceneService.GetCurrentScene();

            if (SelectionService.GetActiveContainer() is SKNode artboard)
            {
                targetParent = artboard;
            }

            if (groupIntoNode != null)
            {
                groupIntoNode.Nodes.Add(sprite);
                SelectionService.Select(groupIntoNode);

                if (groupIntoNode.Parent != targetParent)
                {
                    targetParent.Nodes.Add(groupIntoNode);
                }
            }
            else
            {
                targetParent.Nodes.Add(sprite);
                SelectionService.Select(sprite);
            }

            SelectionService.Selection.UpdateParents(NodeReparentMode.Overflow);

            return sprite;
        }
    }
}