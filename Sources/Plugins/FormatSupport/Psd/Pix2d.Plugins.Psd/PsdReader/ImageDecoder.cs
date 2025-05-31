using SkiaSharp;

namespace Pix2d.Plugins.Psd.PsdReader
{
    public class ImageDecoder
    {
        //public static Bitmap DecodeImage(PsdFile psdFile)
        //{
        //    var bitmap = new Bitmap(psdFile.Columns, psdFile.Rows, PixelFormat.Format32bppArgb);
        //    Parallel.For(0, psdFile.Rows, delegate (int y)
        //    {
        //        var num = y * psdFile.Columns;
        //        for (var i = 0; i < psdFile.Columns; i++)
        //        {
        //            var pos = num + i;
        //            var color = GetColor(psdFile, pos);
        //            lock (bitmap)
        //            {
        //                bitmap.SetPixel(i, y, color);
        //            }
        //        }
        //    });
        //    return bitmap;
        //}

        //public static Bitmap DecodeImage(Layer layer)
        //{
        //    if (layer.Rect.Width == 0 || layer.Rect.Height == 0)
        //    {
        //        return null;
        //    }
        //    var bitmap = new Bitmap(layer.Rect.Width, layer.Rect.Height, PixelFormat.Format32bppArgb);
        //    Parallel.For(0, layer.Rect.Height, delegate (int y)
        //    {
        //        var num = y * layer.Rect.Width;
        //        for (var i = 0; i < layer.Rect.Width; i++)
        //        {
        //            var pos = num + i;
        //            var color = GetColor(layer, pos);
        //            if (layer.SortedChannels.ContainsKey(-2))
        //            {
        //                var color2 = GetColorMask(layer.MaskData, i, y);
        //                color = Color.FromArgb(color.A * color2 / 255, color);
        //            }
        //            lock (bitmap)
        //            {
        //                bitmap.SetPixel(i, y, color);
        //            }
        //        }
        //    });
        //    return bitmap;
        //}

        public static unsafe SKBitmap? DecodeImageToSKBitmap(Layer layer)
        {
            if (layer.Rect.Width == 0 || layer.Rect.Height == 0)
                return null;

            var bitmap = new SKBitmap(new SKImageInfo(layer.Rect.Width, layer.Rect.Height, SKColorType.Bgra8888));
            var ppt = bitmap.GetPixels(out IntPtr len);
            var pixels = new Span<uint>(ppt.ToPointer(), (int)len);
            
            var w = layer.Rect.Width;
            var h = layer.Rect.Height;

            for (var y = 0; y < h; y++)
            {
                var num = y * w;
                for (var i = 0; i < w; i++)
                {
                    var pos = num + i;
                    var color = GetColor(layer, pos);
                    if (layer.SortedChannels.ContainsKey(-2))
                    {
                        var colorMask = GetColorMask(layer.MaskData, i, y);
                        color = color.WithAlpha((byte)(color.Alpha * colorMask / 255));
                    }
                    pixels[y * w + i] = (uint)color;
                }
            }
            return bitmap;
        }

        //public static Bitmap DecodeImage(Layer.Mask mask)
        //{
        //    var layer = mask.Layer;
        //    if (mask.Rect.Width == 0 || mask.Rect.Height == 0)
        //    {
        //        return null;
        //    }
        //    var bitmap = new Bitmap(mask.Rect.Width, mask.Rect.Height, PixelFormat.Format32bppArgb);
        //    Parallel.For(0, layer.Rect.Height, delegate (int y)
        //    {
        //        var num = y * layer.Rect.Width;
        //        for (var i = 0; i < layer.Rect.Width; i++)
        //        {
        //            var num2 = num + i;
        //            var color = Color.FromArgb(mask.ImageData[num2], mask.ImageData[num2], mask.ImageData[num2]);
        //            lock (bitmap)
        //            {
        //                bitmap.SetPixel(i, y, color);
        //            }
        //        }
        //    });
        //    return bitmap;
        //}

        private static SKColor GetColor(PsdFile psdFile, int pos)
        {
            var result = SKColors.White;
            var b = psdFile.ImageData[0][pos];
            var b2 = psdFile.ImageData[1][pos];
            var b3 = psdFile.ImageData[2][pos];
            var b4 = byte.MaxValue;
            if (psdFile.ImageData.Length > 3)
            {
                b4 = psdFile.ImageData[3][pos];
            }
            switch (psdFile.ColorMode)
            {
                case PsdFile.ColorModes.Grayscale:
                case PsdFile.ColorModes.Duotone:
                    result = new SKColor(b, b, b);
                    break;
                case PsdFile.ColorModes.Indexed:
                    {
                        var num = (int)b;
                        result = new SKColor(psdFile.ColorModeData[num], psdFile.ColorModeData[num + 256], psdFile.ColorModeData[num + 512]);
                        break;
                    }
                case PsdFile.ColorModes.RGB:
                    result = new SKColor(b, b2, b3, b4);
                    break;
                case PsdFile.ColorModes.CMYK:
                    result = CMYKToRGB(b, b2, b3, b4);
                    break;
                case PsdFile.ColorModes.Multichannel:
                    result = CMYKToRGB(b, b2, b3, 0);
                    break;
                case PsdFile.ColorModes.Lab:
                    result = LabToRGB(b, b2, b3);
                    break;
            }
            return result;
        }

        private static SKColor GetColor(Layer layer, int pos)
        {
            var color = SKColors.White;
            switch (layer.PsdFile.ColorMode)
            {
                case PsdFile.ColorModes.Grayscale:
                case PsdFile.ColorModes.Duotone:
                    color = new SKColor(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[0].ImageData[pos]);
                    break;
                case PsdFile.ColorModes.Indexed:
                    {
                        var num = (int)layer.SortedChannels[0].ImageData[pos];
                        color = new SKColor(layer.PsdFile.ColorModeData[num], layer.PsdFile.ColorModeData[num + 256], layer.PsdFile.ColorModeData[num + 512]);
                        break;
                    }
                case PsdFile.ColorModes.RGB:
                    color = new SKColor(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos]);
                    break;
                case PsdFile.ColorModes.CMYK:
                    color = CMYKToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos], layer.SortedChannels[3].ImageData[pos]);
                    break;
                case PsdFile.ColorModes.Multichannel:
                    color = CMYKToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos], 0);
                    break;
                case PsdFile.ColorModes.Lab:
                    color = LabToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos]);
                    break;
            }
            if (layer.SortedChannels.TryGetValue(-1, out var channel))
            {
                color = color.WithAlpha(channel.ImageData[pos]);
            }
            return color;
        }

        private static int GetColorMask(Layer.Mask mask, int x, int y)
        {
            var result = 255;
            if (mask.PositionIsRelative)
            {
                x -= mask.Rect.X;
                y -= mask.Rect.Y;
            }
            else
            {
                x = x + mask.Layer.Rect.X - mask.Rect.X;
                y = y + mask.Layer.Rect.Y - mask.Rect.Y;
            }
            if (y >= 0 && y < mask.Rect.Height && x >= 0 && x < mask.Rect.Width)
            {
                var num = y * mask.Rect.Width + x;
                result = (num < mask.ImageData.Length) ? mask.ImageData[num] : byte.MaxValue;
            }
            return result;
        }

        private static SKColor LabToRGB(byte lb, byte ab, byte bb)
        {
            var num = (double)lb;
            var num2 = (double)ab;
            var num3 = (double)bb;
            var num4 = (double)((int)(num / 2.56));
            var num5 = (int)(num2 / 1.0 - 128.0);
            var num6 = (int)(num3 / 1.0 - 128.0);
            var num7 = (num4 + 16.0) / 116.0;
            var num8 = num5 / 500.0 + num7;
            var num9 = num7 - num6 / 200.0;
            num7 = ((Math.Pow(num7, 3.0) > 0.008856) ? Math.Pow(num7, 3.0) : ((num7 - 0.0) / 7.787));
            num8 = ((Math.Pow(num8, 3.0) > 0.008856) ? Math.Pow(num8, 3.0) : ((num8 - 0.0) / 7.787));
            num9 = ((Math.Pow(num9, 3.0) > 0.008856) ? Math.Pow(num9, 3.0) : ((num9 - 0.0) / 7.787));
            var x = 95.047 * num8;
            var y = 100.0 * num7;
            var z = 108.883 * num9;
            return XYZToRGB(x, y, z);
        }

        private static SKColor XYZToRGB(double x, double y, double z)
        {
            var num = x / 100.0;
            var num2 = y / 100.0;
            var num3 = z / 100.0;
            var num4 = num * 3.2406 + num2 * -1.5372 + num3 * -0.4986;
            var num5 = num * -0.9689 + num2 * 1.8758 + num3 * 0.0415;
            var num6 = num * 0.0557 + num2 * -0.204 + num3 * 1.057;
            num4 = ((num4 > 0.0031308) ? (1.055 * Math.Pow(num4, 0.41666666666666669) - 0.055) : (12.92 * num4));
            num5 = ((num5 > 0.0031308) ? (1.055 * Math.Pow(num5, 0.41666666666666669) - 0.055) : (12.92 * num5));
            num6 = ((num6 > 0.0031308) ? (1.055 * Math.Pow(num6, 0.41666666666666669) - 0.055) : (12.92 * num6));
            var num7 = (int)(num4 * 256.0);
            var num8 = (int)(num5 * 256.0);
            var num9 = (int)(num6 * 256.0);
            num7 = ((num7 > 0) ? num7 : 0);
            num7 = ((num7 < 255) ? num7 : 255);
            num8 = ((num8 > 0) ? num8 : 0);
            num8 = ((num8 < 255) ? num8 : 255);
            num9 = ((num9 > 0) ? num9 : 0);
            num9 = ((num9 < 255) ? num9 : 255);
            return new SKColor((byte)num7, (byte)num8, (byte)num9);
        }

        private static SKColor CMYKToRGB(byte c, byte m, byte y, byte k)
        {
            var num = Math.Pow(2.0, 8.0);
            var num2 = (double)c;
            var num3 = (double)m;
            var num4 = (double)y;
            var num5 = (double)k;
            var num6 = 1.0 - num2 / num;
            var num7 = 1.0 - num3 / num;
            var num8 = 1.0 - num4 / num;
            var num9 = 1.0 - num5 / num;
            var num10 = (int)((1.0 - (num6 * (1.0 - num9) + num9)) * 255.0);
            var num11 = (int)((1.0 - (num7 * (1.0 - num9) + num9)) * 255.0);
            var num12 = (int)((1.0 - (num8 * (1.0 - num9) + num9)) * 255.0);
            num10 = ((num10 > 0) ? num10 : 0);
            num10 = ((num10 < 255) ? num10 : 255);
            num11 = ((num11 > 0) ? num11 : 0);
            num11 = ((num11 < 255) ? num11 : 255);
            num12 = ((num12 > 0) ? num12 : 0);
            num12 = ((num12 < 255) ? num12 : 255);
            return new SKColor((byte)num10, (byte)num11, (byte)num12);
        }
    }
}