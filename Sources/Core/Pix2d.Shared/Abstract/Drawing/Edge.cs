using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public struct Edge
{
    public SKPointI P0;
    public SKPointI P1;
    public Edge(int x1, int y1, int x2, int y2)
    {
        P0 = new SKPointI(x1, y1);
        P1 = new SKPointI(x2, y2);
    }

    public bool IsConnectedTo(Edge edge) => P0 == edge.P0 || P0 == edge.P1 || P1 == edge.P0 || P1 == edge.P1;

    public SKPointI GetConnectionPoint(Edge other)
    {
        if (P0 == other.P0)
            return P0;

        if (P1 == other.P0)
            return P1;

        if (P0 == other.P1)
            return P0;

        if (P1 == other.P1)
            return P1;

        return default(SKPointI);
    }

    public SKPointI GetFreePoint(Edge other)
    {
        if (P0 != other.P0)
            return P0;

        if (P1 != other.P0)
            return P1;

        if (P0 != other.P1)
            return P0;

        if (P1 != other.P1)
            return P1;

        return default(SKPointI);
    }
}