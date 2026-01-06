using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Abstract.Selection;

public interface INodesSelection : IDisposable
{
    SKNode[] Nodes { get; set; }
    int NodesCount { get; }
    SKRect Bounds { get; }

    void Invalidate(bool raiseNotificationEvents = true);
    void ResetFrame();
    void Delete();
    void MoveBy(int x, int y);
    void Hide();
    void SendBackward();
    void BringForward();
}