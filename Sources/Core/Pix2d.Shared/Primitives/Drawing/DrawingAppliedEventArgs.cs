namespace Pix2d.Primitives.Drawing;

public class DrawingAppliedEventArgs(bool saveToUndo) : EventArgs
{
    public bool SaveToUndo { get; set; } = saveToUndo;
}