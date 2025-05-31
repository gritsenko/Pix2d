using SkiaSharp;

namespace SkiaNodes.Serialization;

public interface IDataStorage
{
    void SetEntry(string id, SKBitmap data);
    SKBitmap GetEntry(string id);
}