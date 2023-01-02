using System;
using System.Collections.Generic;
using Pix2d.Plugins.Tmx.Tools;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Tmx
{
    public class TileMapNode : SKNode
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileHeight { get; set; }
        public int TileWidth { get; set; }
        public TileSetCollection TileSets { get; set; }
        public int[,] Tiles { get; set; }


        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            var paint = new SKPaint();
            paint.IsStroke = true;
            paint.Color = SKColors.Green;

            var tpaint = new SKPaint();
            tpaint.Color = SKColors.Red;

            for (int j = 0; j < Height; j++)
            for (int i = 0; i < Width; i++)
            {
                var tile = Tiles[i, j];
                var x = (float)(i * TileWidth);
                var y = (float)(j * TileHeight);
                //canvas.DrawRect(x, y, TileWidth, TileHeight, paint);
                //canvas.DrawText(tile.ToString(), x+3, y+3, tpaint);
                DrawTile(canvas, x, y, tile);
            }
        }

        private void DrawTile(SKCanvas canvas, float x, float y, int tile)
        {
            if (TileSets.TryGetTileBitmap(tile, out var bm, out var sourceRect))
            {
                canvas.DrawBitmap(bm, sourceRect, new SKRect(x, y, x + TileWidth, y + TileHeight));
            }
        }
    }
}
