using System;
using System.Collections.Generic;
using Pix2d;
using SkiaSharp;

namespace PixelMixel.Modules.PixelCanvas.Tools
{
    public class FloodFiller
    {
        private readonly SKColor[] _pixels;
        private readonly SKSizeI _size;
        private bool[] _pixelsChecked;
        private FloodFillRangeQueue _ranges;
        private SKColor _targetColor;
        private SKColor _startColor;

        public SKColor[] Pixels => _pixels;
        public SKColor[] FilledPixels { get; }

        public FloodFiller(SKColor[] pixels, SKSizeI size)
        {
            _pixels = pixels;
            FilledPixels = new SKColor[pixels.Length];
            _size = size;
            _pixelsChecked = new bool[_pixels.Length];
            _ranges = new FloodFillRangeQueue(((_size.Width + _size.Height) / 2) * 5);
        }
        private int GetIndex(ref int x, ref int y) => x + y * _size.Width;

        private bool CheckPixel(ref int index)
        {
            _pixelsChecked[index] = true;
            return _pixels[index] == _startColor;
        } 

        public void FloodFill(SKPointI pt, SKColor targetColor)
        {
            _targetColor = targetColor;
            //***Get starting color.
            int x = pt.X;
            int y = pt.Y;
            int idx = GetIndex(ref x, ref y);
            _startColor = _pixels[idx];


            //***Do first call to floodfill.
            LinearFill(ref x, ref y);

            //***Call floodfill routine while floodfill ranges still exist 
            //on the queue
            while (_ranges.Count > 0)
            {
                //**Get Next Range Off the Queue
                FloodFillRange range = _ranges.Dequeue();

                //**Check Above and Below Each Pixel in the Floodfill Range
                int downPxIdx = (_size.Width * (range.Y + 1)) + range.StartX;
                //CoordsToPixelIndex(lFillLoc,y+1);
                int upPxIdx = (_size.Width * (range.Y - 1)) + range.StartX;
                //CoordsToPixelIndex(lFillLoc, y - 1);
                int upY = range.Y - 1;//so we can pass the y coord by ref
                int downY = range.Y + 1;
                int tempIdx;
                for (int i = range.StartX; i <= range.EndX; i++)
                {
                    //*Start Fill Upwards
                    //if we're not above the top of the bitmap and the pixel 
                    //above this one is within the color tolerance
                    tempIdx = GetIndex(ref i, ref upY);
                    if (range.Y > 0 && (!_pixelsChecked[upPxIdx]) &&
                        CheckPixel(ref tempIdx))
                        LinearFill(ref i, ref upY);

                    //*Start Fill Downwards
                    //if we're not below the bottom of the bitmap and 
                    //the pixel below this one is
                    //within the color tolerance
                    tempIdx = GetIndex(ref i, ref downY);
                    if (range.Y < (_size.Height - 1) && (!_pixelsChecked[downPxIdx])
                        && CheckPixel(ref tempIdx))
                        LinearFill(ref i, ref downY);
                    downPxIdx++;
                    upPxIdx++;
                }

            }
        }

        /// <summary>
        /// Finds the furthermost left and right boundaries of the fill area
        /// on a given y coordinate, starting from a given x coordinate, 
        /// filling as it goes.
        /// Adds the resulting horizontal range to the queue of floodfill ranges,
        /// to be processed in the main loop.
        /// </summary>
        /// <param name="x">The x coordinate to start from.</param>
        /// <param name="y">The y coordinate to check at.</param>
        void LinearFill(ref int x, ref int y)
        {
            //cache some bitmap and fill info in local variables for 
            //a little extra speed
            SKColor[] pixels = _pixels;
            bool[] pixelsChecked = _pixelsChecked;
            SKColor targetColor = _targetColor;
            int bitmapPixelFormatSize = 1;
            int bitmapWidth = _size.Width;

            //***Find Left Edge of Color Area
            int lFillLoc = x; //the location to check/fill on the left
            int idx = GetIndex(ref x, ref y);
            //the byte index of the current location
            int pxIdx = (bitmapWidth * y) + x;//CoordsToPixelIndex(x,y);
            while (true)
            {
                //**fill with the color
                pixels[idx] = targetColor;
                FilledPixels[idx] = targetColor;
                //**indicate that this pixel has already been checked and filled
                pixelsChecked[pxIdx] = true;
                //**de-increment
                lFillLoc--;     //de-increment counter
                pxIdx--;        //de-increment pixel index
                idx -= bitmapPixelFormatSize;//de-increment byte index
                //**exit loop if we're at edge of bitmap or color area
                if (lFillLoc < 0 || (pixelsChecked[pxIdx]) || !CheckPixel(ref idx))
                    break;
            }
            lFillLoc++;

            //***Find Right Edge of Color Area
            int rFillLoc = x; //the location to check/fill on the left
            idx = GetIndex(ref x, ref y);
            pxIdx = (bitmapWidth * y) + x;
            while (true)
            {
                //**fill with the color
                pixels[idx] = targetColor;
                FilledPixels[idx] = targetColor;
                //**indicate that this pixel has already been checked and filled
                pixelsChecked[pxIdx] = true;
                //**increment
                rFillLoc++;     //increment counter
                pxIdx++;        //increment pixel index
                idx += bitmapPixelFormatSize;//increment byte index
                //**exit loop if we're at edge of bitmap or color area
                if (rFillLoc >= bitmapWidth || pixelsChecked[pxIdx] ||
                    !CheckPixel(ref idx))
                    break;

            }
            rFillLoc--;

            //add range to queue
            var r = new FloodFillRange(lFillLoc, rFillLoc, y);
            _ranges.Enqueue(r);
        }

        public byte[] GetPixelBytes()
        {
            var pixels = FilledPixels;
            var bytes = new byte[pixels.Length * 4];
            var di = 0;
            for (var i = 0; i < pixels.Length; i++, di = i * 4)
            {
                switch (Pix2DAppSettings.ColorType)
                {
                    case SKColorType.Bgra8888:
                        bytes[di] = pixels[i].Blue;
                        bytes[di+1] = pixels[i].Green;
                        bytes[di+2] = pixels[i].Red;
                        bytes[di+3] = pixels[i].Alpha;
                        break;
                    case SKColorType.Rgba8888:
                        bytes[di] = pixels[i].Red;
                        bytes[di+1] = pixels[i].Green;
                        bytes[di+2] = pixels[i].Blue;
                        bytes[di+3] = pixels[i].Alpha;
                        break;
                    default:
                        throw new Exception("Sorry, I don't support this color type");
                }
            }
            return bytes;
        }
    }

    public class FloodFillRangeQueue : Queue<FloodFillRange>
    {
        public FloodFillRangeQueue(int capacity) : base(capacity)
        {
        }
    }

    public class FloodFillRange
    {
        public FloodFillRange(int startX, int endX, int y)
        {
            StartX = startX;
            EndX = endX;
            Y = y;
        }

        public int StartX { get; set; }
        public int EndX { get; set; }
        public int Y { get; set; }
    }
}