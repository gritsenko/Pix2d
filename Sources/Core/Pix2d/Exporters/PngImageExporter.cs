using System;
using System.Collections.Generic;
using System.IO;
using Pix2d.Abstract.Export;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters;

public class PngImageExporter : IExporter
{
    public Stream Export(IEnumerable<SKNode> nodesToExport, double scale = 1)
    {
        try
        {
            var skBitmap = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
            return skBitmap.ToPngStream();
        }
        catch (Exception e)
        {
            IoC.Get<IDialogService>().Alert("There's nothing to Export!", "Export");
            Logger.Log(e.Message);
        }

        return null;
    }
}