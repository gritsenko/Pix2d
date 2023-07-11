using System;
using Pix2d.Abstract.Tools;
using SkiaSharp;

namespace Pix2d.Tools
{
    public class TextTool : ObjectCreationTool
    {
        public static ToolSettings ToolSettings { get; } = new()
        {
            DisplayName = "Text tool",
            HotKey = null,
        };


        protected override void CreateObjectCore(SKRect destRect)
        {
            if(destRect.Width < 1 || Math.Abs(destRect.Height) < 1)
                destRect = new SKRect(destRect.Left, destRect.Top, destRect.Left + 64, destRect.Top + 64);
            ObjectCreationService.CreateText(destRect);
        }

        public override string DisplayName => ToolSettings.DisplayName;
    }
}