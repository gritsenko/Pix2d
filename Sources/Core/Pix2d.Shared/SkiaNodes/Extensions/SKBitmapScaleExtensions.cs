using System;
using SkiaSharp;

namespace SkiaNodes.Extensions
{
    public static class SKBitmapScaleExtensions
    {
        static unsafe Span<int> GetPixelsSpan(SKBitmap bm) => new Span<int>((void*)bm.GetPixels(out var length), (int)length / sizeof(int));

        public static SKBitmap Scale6x(this SKBitmap srcBitmap)
        {
            var info = new SKImageInfo(
                srcBitmap.Width * 6,
                srcBitmap.Height * 6,
                SKColorType.Bgra8888);
            var result = new SKBitmap(info);
            srcBitmap.Scale6x(result);
            return result;
        }
        public static void Scale6x(this SKBitmap srcBitmap, SKBitmap destBitmap)
        {
            var wsrc = srcBitmap.Width;
            var hsrc = srcBitmap.Height;

            var wdest = destBitmap.Width;
            var hdest = destBitmap.Height;

            destBitmap.Clear();

            var src =  srcBitmap.GetPixelSpan();
            var dest = GetPixelsSpan(destBitmap);

            for (int y = 0; y < hsrc; y++)
                for (int x = 0; x < wsrc; x++)
                    dest[x + y * wdest] = src[x + y * wsrc];

            int GetDestPixel(int x, int y, Span<int> destSpan) => x < 0 || y < 0 || x >= wdest || y >= hdest ? 0 : destSpan[x + y * wdest];
            void SetDestPixel(int x, int y, int col, Span<int> destSpan)
            {
                if (x < 0 || y < 0 || x >= wdest || y >= hdest) return;
                destSpan[x + y * wdest] = col;
            }

            void ScalePixel2x(int x, int y, int x0, int y0, Span<int> destSpan)
            {
                var B = GetDestPixel(x, y - 1, destSpan);
                var D = GetDestPixel(x - 1, y, destSpan);
                var E = GetDestPixel(x, y, destSpan);
                var F = GetDestPixel(x + 1, y, destSpan);
                var H = GetDestPixel(x, y + 1, destSpan);

                var E0 = E;
                var E1 = E;
                var E2 = E;
                var E3 = E;
                if (B != H && D != F)
                {
                    E0 = D == B ? D : E;
                    E1 = B == F ? F : E;
                    E2 = D == H ? D : E;
                    E3 = H == F ? F : E;
                }
                SetDestPixel(x0, y0, E0, destSpan);
                SetDestPixel(x0 + 1, y0, E1, destSpan);
                SetDestPixel(x0, y0 + 1, E2, destSpan);
                SetDestPixel(x0 + 1, y0 + 1, E3, destSpan);
            }

            void ScalePixel3x(int x, int y, int x0, int y0, Span<int> destSpan)
            {
                var A = GetDestPixel(x - 1, y - 1, destSpan);
                var B = GetDestPixel(x, y - 1, destSpan);
                var C = GetDestPixel(x + 1, y - 1, destSpan);

                var D = GetDestPixel(x - 1, y, destSpan);
                var E = GetDestPixel(x, y, destSpan);
                var F = GetDestPixel(x + 1, y, destSpan);

                var G = GetDestPixel(x - 1, y + 1, destSpan);
                var H = GetDestPixel(x, y + 1, destSpan);
                var I = GetDestPixel(x + 1, y + 1, destSpan);

                var E0 = E;
                var E1 = E;
                var E2 = E;
                var E3 = E;
                var E4 = E;
                var E5 = E;
                var E6 = E;
                var E7 = E;
                var E8 = E;

                if (B != H && D != F)
                {
                    E0 = D == B ? D : E;
                    E1 = (D == B && E != C) || (B == F && E != A) ? B : E;
                    E2 = B == F ? F : E;
                    E3 = (D == B && E != G) || (D == H && E != A) ? D : E;
                    E4 = E;
                    E5 = (B == F && E != I) || (H == F && E != C) ? F : E;
                    E6 = D == H ? D : E;
                    E7 = (D == H && E != I) || (H == F && E != G) ? H : E;
                    E8 = H == F ? F : E;
                }
                SetDestPixel(x0, y0, E0, destSpan);
                SetDestPixel(x0 + 1, y0, E1, destSpan);
                SetDestPixel(x0 + 2, y0, E2, destSpan);

                SetDestPixel(x0, y0 + 1, E3, destSpan);
                SetDestPixel(x0 + 1, y0 + 1, E4, destSpan);
                SetDestPixel(x0 + 2, y0 + 1, E5, destSpan);

                SetDestPixel(x0, y0 + 2, E6, destSpan);
                SetDestPixel(x0 + 1, y0 + 2, E7, destSpan);
                SetDestPixel(x0 + 2, y0 + 2, E8, destSpan);
            }

            var s = 3;
            var w0 = wsrc;
            var h0 = hsrc;
            var w1 = w0 * s;
            var h1 = h0 * s;
            for (int y = h0 - 1, yd = h1 - s; y >= 0; y--, yd -= s)
                for (int x = w0 - 1, xd = w1 - s; x >= 0; x--, xd -= s)
                    ScalePixel3x(x, y, xd, yd, dest);

            s = 2;
            w0 = w1;
            h0 = h1;
            w1 = w0 * s;
            h1 = h0 * s;
            for (int y = h0 - 1, yd = h1 - s; y >= 0; y--, yd -= s)
                for (int x = w0 - 1, xd = w1 - s; x >= 0; x--, xd -= s)
                    ScalePixel2x(x, y, xd, yd, dest);

            destBitmap.NotifyPixelsChanged();
        }

        public static void Scale2x(this SKBitmap srcBitmap, SKBitmap destBitmap)
        {
            var wsrc = srcBitmap.Width;
            var hsrc = srcBitmap.Height;
            var s = 2;

            var wdest = destBitmap.Width;
            var hdest = destBitmap.Height;

            destBitmap.Clear();

            var src = GetPixelsSpan(srcBitmap);
            var dest = GetPixelsSpan(destBitmap);

            int GetSrcPixel(int x, int y, Span<int> srcSpan) => x < 0 || y < 0 || x >= wsrc || y >= hsrc ? 0 : srcSpan[x + y * wsrc];

            void SetDestPixel(int x, int y, int col, Span<int> destSpan)
            {
                if (x < 0 || y < 0 || x >= hdest || y >= hdest) return;
                destSpan[x + y * wdest] = col;
            }

            void ScalePixel(int x, int y, int x0, int y0, Span<int> srcSpan, Span<int> destSpan)
            {
                var B = GetSrcPixel(x, y - 1, srcSpan);
                var D = GetSrcPixel(x - 1, y, srcSpan);
                var E = GetSrcPixel(x, y, srcSpan);
                var F = GetSrcPixel(x + 1, y, srcSpan);
                var H = GetSrcPixel(x, y + 1, srcSpan);

                var E0 = E;
                var E1 = E;
                var E2 = E;
                var E3 = E;
                if (B != H && D != F)
                {
                    E0 = D == B ? D : E;
                    E1 = B == F ? F : E;
                    E2 = D == H ? D : E;
                    E3 = H == F ? F : E;
                }
                SetDestPixel(x0, y0, E0, destSpan);
                SetDestPixel(x0 + 1, y0, E1, destSpan);
                SetDestPixel(x0, y0 + 1, E2, destSpan);
                SetDestPixel(x0 + 1, y0 + 1, E3, destSpan);
            }

            for (int y = 0, yd = 0; y < hsrc; y++, yd += s)
                for (int x = 0, xd = 0; x < wsrc; x++, xd += s)
                    ScalePixel(x, y, xd, yd, src, dest);

            destBitmap.NotifyPixelsChanged();
        }

        public static void Scale3x(SKBitmap srcBitmap, SKBitmap destBitmap)
        {
            var wsrc = srcBitmap.Width;
            var hsrc = srcBitmap.Height;
            var s = 3;

            var wdest = destBitmap.Width;
            var hdest = destBitmap.Height;

            destBitmap.Clear();

            var src = GetPixelsSpan(srcBitmap);
            var dest = GetPixelsSpan(destBitmap);

            int GetSrcPixel(int x, int y, Span<int> srcSpan) => x < 0 || y < 0 || x >= wsrc || y >= hsrc ? 0 : srcSpan[x + y * wsrc];
            void SetDestPixel(int x, int y, int col, Span<int> destSpan)
            {
                if (x < 0 || y < 0 || x >= hdest || y >= hdest) return;
                destSpan[x + y * wdest] = col;
            }

            void ScalePixel(int x, int y, int x0, int y0, Span<int> srcSpan, Span<int> destSpan)
            {
                var A = GetSrcPixel(x - 1, y - 1, srcSpan);
                var B = GetSrcPixel(x, y - 1, srcSpan);
                var C = GetSrcPixel(x + 1, y - 1, srcSpan);

                var D = GetSrcPixel(x - 1, y, srcSpan);
                var E = GetSrcPixel(x, y, srcSpan);
                var F = GetSrcPixel(x + 1, y, srcSpan);

                var G = GetSrcPixel(x - 1, y + 1, srcSpan);
                var H = GetSrcPixel(x, y + 1, srcSpan);
                var I = GetSrcPixel(x + 1, y + 1, srcSpan);

                var E0 = E;
                var E1 = E;
                var E2 = E;
                var E3 = E;
                var E4 = E;
                var E5 = E;
                var E6 = E;
                var E7 = E;
                var E8 = E;

                if (B != H && D != F)
                {
                    E0 = D == B ? D : E;
                    E1 = (D == B && E != C) || (B == F && E != A) ? B : E;
                    E2 = B == F ? F : E;
                    E3 = (D == B && E != G) || (D == H && E != A) ? D : E;
                    E4 = E;
                    E5 = (B == F && E != I) || (H == F && E != C) ? F : E;
                    E6 = D == H ? D : E;
                    E7 = (D == H && E != I) || (H == F && E != G) ? H : E;
                    E8 = H == F ? F : E;
                }
                SetDestPixel(x0, y0, E0, destSpan);
                SetDestPixel(x0 + 1, y0, E1, destSpan);
                SetDestPixel(x0 + 2, y0, E2, destSpan);

                SetDestPixel(x0, y0 + 1, E3, destSpan);
                SetDestPixel(x0 + 1, y0 + 1, E4, destSpan);
                SetDestPixel(x0 + 2, y0 + 1, E5, destSpan);

                SetDestPixel(x0, y0 + 2, E6, destSpan);
                SetDestPixel(x0 + 1, y0 + 2, E7, destSpan);
                SetDestPixel(x0 + 2, y0 + 2, E8, destSpan);
            }

            for (int y = 0, yd = 0; y < hsrc; y++, yd += s)
                for (int x = 0, xd = 0; x < wsrc; x++, xd += s)
                    ScalePixel(x, y, xd, yd, src, dest);

            destBitmap.NotifyPixelsChanged();
        }

    }
}
