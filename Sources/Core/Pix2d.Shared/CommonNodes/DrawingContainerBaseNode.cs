using System;
using System.Collections.Generic;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes
{
    public class DrawingContainerBaseNode : SKNode, IContainerNode
    {
        private GridNode _grid;
        private bool _showGrid;
        readonly Dictionary<int, SKBitmap> _checkerBoardCache = new Dictionary<int, SKBitmap>();

        public SKSize GridCellSize
        {
            get => _grid.CellSize;
            set => _grid.CellSize = value;
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                _grid.IsVisible = value;
            }
        }

        public SKColor BackgroundColor { get; set; } = SKColors.White;
        public bool UseBackgroundColor { get; set; }

        public DrawingContainerBaseNode()
        {
            _grid = new GridNode();
            _grid.Size = this.Size;
            var adorner = SkiaNodes.AdornerLayer.GetAdornerLayer(this);
            adorner.Nodes.Add(_grid);
            _grid.IsVisible = _showGrid;
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            _checkerBoardCache.Clear();
            _grid.Size = Size;
        }

        private void DrawCheckerboard(SKCanvas canvas, ViewPort vp)
        {
            canvas.DrawBitmap(GetCheckerBoardBitmap(canvas, vp), 0, 0);

        }

        private SKBitmap GetCheckerBoardBitmap(SKCanvas canvas, ViewPort vp)
        {
            var cellSize = GridCellSize.Width < 1 ? 8 : GridCellSize.Width; //ensure that cell size is valid

            var mp = (int)(vp.Zoom < 4 ? cellSize : 1);

            return _checkerBoardCache.TryGetValue((int) (mp * cellSize), out var bm)
                ? bm
                : RenderCheckerboardToCache(canvas, mp, (int)cellSize);
        }

        private SKBitmap RenderCheckerboardToCache(SKCanvas canvas, int mp, int cellSize)
        {
            var bm = new SKBitmap((int)Size.Width, (int)Size.Height);

            using (var cc = new SKCanvas(bm))
            using (var paint = canvas.GetSolidFillPaint(SKColor.Parse("d2d2d2")))
            {
                cc.Clear(SKColors.White);
                for (var y = 0; y < Size.Height; y += cellSize)
                {
                    var offset = Math.Floor((double)(y / cellSize)) % 2 > 0 ? 0 : cellSize;

                    for (var x = 0; x < Size.Width; x += cellSize * 2)
                    {
                        cc.DrawRect(x + offset, y, cellSize, cellSize, paint);
                    }
                }
                cc.Flush();
            }

            _checkerBoardCache[mp * cellSize] = bm;
            return bm;
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            if (vp.Settings.RenderAdorners && !UseBackgroundColor)
                DrawCheckerboard(canvas, vp);
        }

        public virtual void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        {
            throw new NotImplementedException();
        }

        public virtual void Crop(SKRect targetBounds)
        {
            throw new NotImplementedException();
        }
    }
} 