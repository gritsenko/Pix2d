using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Common.Drawing;

public static class Algorithms
{
    private static void Swap<T>(ref T lhs, ref T rhs)
    {
        T temp;
        temp = lhs;
        lhs = rhs;
        rhs = temp;
    }

    /// <summary>
    /// The plot function delegate
    /// </summary>
    /// <param name="x">The x co-ord being plotted</param>
    /// <param name="y">The y co-ord being plotted</param>
    /// <returns>True to continue, false to stop the algorithm</returns>
    public delegate bool PlotFunction(int x, int y);

    /// <summary>
    /// Plot the line from (x0, y0) to (x1, y10
    /// </summary>
    /// <param name="x0">The start x</param>
    /// <param name="y0">The start y</param>
    /// <param name="x1">The end x</param>
    /// <param name="y1">The end y</param>
    /// <param name="plot">The plotting function (if this returns false, the algorithm stops early)</param>
    public static void Line(int x0, int y0, int x1, int y1, PlotFunction plot)
    {
        bool step = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
        if (step)
        {
            Swap<int>(ref x0, ref y0);
            Swap<int>(ref x1, ref y1);
        }
        if (x0 > x1)
        {
            Swap<int>(ref x0, ref x1);
            Swap<int>(ref y0, ref y1);
        }
        int dX = (x1 - x0), dY = Math.Abs(y1 - y0), err = (dX / 2), ystep = (y0 < y1 ? 1 : -1), y = y0;

        for (int x = x0; x <= x1; ++x)
        {
            if (!(step ? plot(y, x) : plot(x, y))) continue;
            err = err - dY;
            if (err < 0)
            {
                y += ystep;
                err += dX;
            }
        }
    }

    public static void LineNotSwapped(int x, int y, int x2, int y2, PlotFunction plot)
    {
        int w = x2 - x;
        int h = y2 - y;
        int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
        if (w < 0) dx1 = -1; else if (w > 0) dx1 = 1;
        if (h < 0) dy1 = -1; else if (h > 0) dy1 = 1;
        if (w < 0) dx2 = -1; else if (w > 0) dx2 = 1;
        int longest = Math.Abs(w);
        int shortest = Math.Abs(h);
        if (!(longest > shortest))
        {
            longest = Math.Abs(h);
            shortest = Math.Abs(w);
            if (h < 0) dy2 = -1; else if (h > 0) dy2 = 1;
            dx2 = 0;
        }
        int numerator = longest >> 1;
        for (int i = 0; i <= longest; i++)
        {
            plot(x, y);
            numerator += shortest;
            if (!(numerator < longest))
            {
                numerator -= longest;
                x += dx1;
                y += dy1;
            }
            else
            {
                x += dx2;
                y += dy2;
            }
        }
    }

    public static void LineDda(int x1, int y1, int x2, int y2, Action<int, int> plot)
    {
        var dx = (float)x2 - x1;
        var dy = (float)y2 - y1;
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);
        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
        {
            plot(x1, y1);
            return;
        }

        int maxSteps;

        maxSteps = (int)(adx >= ady ? adx : ady);

        dx = dx / maxSteps;
        dy = dy / maxSteps;


        float x = x1;
        float y = y1;

        var i = 1;
        while (i <= maxSteps)
        {
            plot((int)x, (int)y);
            x = x + dx;
            y = y + dy;
            i = i + 1;
        }
    }

    public static bool PointInPolygon(SKPoint p, SKPoint[] pts)
    {

        var y = p.Y;
        var x = p.X;
        var polyCorners = pts.Length;
        int i, j = polyCorners - 1;
        bool oddNodes = false;

        for (i = 0; i < polyCorners; i++)
        {
            if (pts[i].Y < y && pts[j].Y >= y
                || pts[j].Y < y && pts[i].Y >= y)
            {
                if (pts[i].X + (y - pts[i].Y) / (pts[j].Y - pts[i].Y) * (pts[j].X - pts[i].X) < x)
                {
                    oddNodes = !oddNodes;
                }
            }
            j = i;
        }

        return oddNodes;
    }

    public static void FillPolygon(SKPoint[] pts, Action<int, int> plot)
    {
        //  public-domain code by Darel Rex Finley, 2007
        var imageLeft = pts.Min(x => x.X);
        var imageTop = pts.Min(x => x.Y);

        var imageRight = pts.Max(x => x.X);
        var imageBot = pts.Max(x => x.Y);


        int nodes, pixelX, pixelY, i, j, swap;
        int[] nodeX = new int[pts.Length];

        //  Loop through the rows of the image.
        for (pixelY = (int)imageTop; pixelY < imageBot; pixelY++)
        {

            //  Build a list of nodes.
            nodes = 0;
            j = pts.Length - 1;
            for (i = 0; i < pts.Length; i++)
            {
                if (pts[i].Y < (double)pixelY && pts[j].Y >= (double)pixelY
                    || pts[j].Y < (double)pixelY && pts[i].Y >= (double)pixelY)
                {
                    nodeX[nodes++] = (int)(pts[i].X + (pixelY - pts[i].Y) / (pts[j].Y - pts[i].Y)
                        * (pts[j].X - pts[i].X));
                }
                j = i;
            }

            //  Sort the nodes, via a simple “Bubble” sort.
            i = 0;
            while (i < nodes - 1)
            {
                if (nodeX[i] > nodeX[i + 1])
                {
                    swap = nodeX[i];
                    nodeX[i] = nodeX[i + 1];
                    nodeX[i + 1] = swap;
                    if (i > 0) i--;
                }
                else
                {
                    i++;
                }
            }

            //  Fill the pixels between node pairs.
            for (i = 0; i < nodes; i += 2)
            {
                if (nodeX[i] >= imageRight) break;
                if (nodeX[i + 1] > imageLeft)
                {
                    if (nodeX[i] < imageLeft) nodeX[i] = (int)imageLeft;
                    if (nodeX[i + 1] > imageRight) nodeX[i + 1] = (int)imageRight;
                    for (pixelX = nodeX[i]; pixelX < nodeX[i + 1]; pixelX++) plot(pixelX + 1, pixelY);
                }
            }
        }
    }
    
    public static SKPath GetContour(HashSet<SKPointI> points, byte[] pixelBuff, SKRectI bounds, SKPointI offset, SKSizeI size)
    {
        bool IsPSet(int x, int y)
        {
            if (x < bounds.Left || y < bounds.Top || x > bounds.Right || y > bounds.Bottom)
                return false;

            var index = x + offset.X + (y + offset.Y) * size.Width;

            return index < pixelBuff.Length && pixelBuff[index] > 0;
        }

        var edges = new List<Edge>();
        var vertices = new Dictionary<SKPointI, List<int>>();

        void AddVertex(int x, int y, int index)
        {
            var vertex = new SKPointI(x, y);
            if (!vertices.ContainsKey(vertex))
            {
                vertices.Add(vertex, new List<int>());
            }
            
            vertices[vertex].Add(index);
        }

        void AddEdge(int x0, int y0, int x1, int y1)
        {
            var index = edges.Count;
            edges.Add(new Edge(x0, y0, x1, y1));
            AddVertex(x0, y0, index);
            AddVertex(x1, y1, index);
        }

        foreach (var spt in points)
        {
            var x = spt.X;
            var y = spt.Y;

            if (!IsPSet(x, y - 1)) AddEdge(x, y, x + 1, y);
            if (!IsPSet(x, y + 1)) AddEdge(x, y + 1, x + 1, y + 1);
            if (!IsPSet(x - 1, y)) AddEdge(x, y, x, y + 1);
            if (!IsPSet(x + 1, y)) AddEdge(x + 1, y, x + 1, y + 1);
        }
        
        var contours = new List<List<SKPoint>>();
        while (vertices.Any())
        {
            var (firstKey, firstValue) = vertices.First();
            if (!firstValue.Any())
            {
                // Because of how filling algorithm works, this can happen sometimes when
                // contour is self-intersecting.
                vertices.Remove(firstKey);
                continue;
            }
            
            var contour = new List<SKPoint> {firstKey};
            var nextEdgeIndex = firstValue.Last();
            firstValue.RemoveAt(firstValue.Count - 1);
            var nextPoint = firstKey;

            while (nextEdgeIndex >= 0)
            {
                var nextEdge = edges[nextEdgeIndex];
                nextPoint = nextEdge.P0 == nextPoint ? nextEdge.P1 : nextEdge.P0;

                var nextEdgesList = vertices[nextPoint];
                nextEdgesList.Remove(nextEdgeIndex);
                
                contour.Add(nextPoint);

                if (nextEdgesList.Any())
                {
                    nextEdgeIndex = nextEdgesList.Last();
                    nextEdgesList.RemoveAt(nextEdgesList.Count - 1);
                }
                else
                {
                    nextEdgeIndex = -1;
                }

                if (!nextEdgesList.Any())
                {
                    vertices.Remove(nextPoint);
                }
            }
            
            contours.Add(contour);
        }
        
        var path = new SKPath();
        foreach (var contour in contours)
        {
            path.AddPoly(contour.ToArray());
        }

        return path;
    }   
}