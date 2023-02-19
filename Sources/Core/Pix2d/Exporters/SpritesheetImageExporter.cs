using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters;

public class SpritesheetImageExporter : SKNodeExporterBase
{
    public int MaxColumns { get; set; } = int.MaxValue;
    protected override Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale)
    {
        var framesArr = frames.ToArray();

        var w = (int)(framesArr[0].Width * MaxColumns * scale);

        var rows = Math.Ceiling(framesArr.Length / (double)MaxColumns);

        var h = (int) (framesArr[0].Height * rows * scale);

        using (var bitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul))
        {
            var vp = new ViewPort(w, h);
            vp.Settings.RenderAdorners = false;
            vp.ShowArea(new SKRect(0, 0, w, h));

            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear();

                var x = 0;
                var y = 0;
                var paint = new SKPaint()
                {
                    FilterQuality = SKFilterQuality.None
                };

                foreach (var frame in framesArr)
                {
                    var fw = (int)(frame.Width * scale);
                    var fh = (int)(frame.Height * scale);

                    var destRect = new SKRect(x, y, x + fw, y + fh);
                    canvas.DrawBitmap(frame, destRect, paint);

                    x += fw;

                    if (x < w) continue;

                    x = 0;
                    y += fh;
                }
                canvas.Flush();
            }
            return bitmap.ToPngStream();
        }

    }
}