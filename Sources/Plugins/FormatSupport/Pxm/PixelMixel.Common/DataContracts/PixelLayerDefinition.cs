using System;
using PixelMixel.Common;

namespace PixelMixel.Modules.PixelCanvas.Layers
{
    public class PixelLayerDefinition
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public int Order { get; set; }
        public IntSize Size { get; set; }
        public IntPoint Offset { get; set; }

        [Obsolete]
        public PixelDefinition[] Pixels { get; set; }
        public float Opacity { get; set; }

        public LayerEffectDefinition[] Effects { get; set; }
        public byte[] PixelsBytes { get; set; }

        public FrameDefinition[] Frames { get; set; }

        public int? BlendMode { get; set; }
    }
}