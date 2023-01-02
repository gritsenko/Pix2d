using PixelMixel.Common;

namespace PixelMixel.Modules.PixelCanvas.Layers
{
    public class TileSetDefinition : ProjectNodeDefinition
    {
        public IntPoint ActiveTileCoords { get; set; }

        public IntSize Size { get; set; }

        public IntSize TileSize { get; set; }

        public ArtWorkDefinition[] ArtWorks { get; set; }
    }
}