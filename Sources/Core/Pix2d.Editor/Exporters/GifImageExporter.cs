using System.Collections.Generic;
using System.IO;
using Pix2d.Common.Gif;
using SkiaSharp;

namespace Pix2d.Exporters
{
    public class GifImageExporter : SKNodeExporterBase
    {

        protected override Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale)
        {
            var frameDelay = (int) (100 / frameRate);
            //var e = new MyGifEncoder(frameDelay, (int) scale);
            var e = new AnimatedGifEncoder(frameDelay, (int) scale);
            foreach (var frame in frames)
            {
                e.AddFrame(frame);
            }

            e.Encode();
            return e.GetResultStream();
        }
    }
}