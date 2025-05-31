using PixelMixel.Common;

namespace PixelMixel.Modules.PixelCanvas.Layers
{
    public class ArtWorkDefinition : ProjectNodeDefinition
    {
        public IntSize Size { get; set; }

        public PixelLayerDefinition[] Layers { get; set; }

        public StoryBoardDefinition Storyboard { get; set; }

    }
}