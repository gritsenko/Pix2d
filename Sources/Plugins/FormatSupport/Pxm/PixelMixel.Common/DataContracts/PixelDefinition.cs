namespace PixelMixel.Modules.PixelCanvas.Layers
{
    public class PixelDefinition
    {
        public byte[] Color { get; set; }

        public PixelDefinition() { }
        public PixelDefinition(byte[] color)
        {
            Color = color;
        }
    }

}