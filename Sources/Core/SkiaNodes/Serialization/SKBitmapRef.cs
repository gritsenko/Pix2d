using System;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public class SKBitmapRef
{
    public string Id { get; set; } = string.Empty;

    public SKBitmapRef()
    {
    }

    public SKBitmapRef(SKBitmap sourceObject, IDataStorage? dataStorage)
    {
        if (dataStorage != null)
        {
            Id = dataStorage.GetOrCreateId(sourceObject);
        }
        else
        {
            Id = Guid.NewGuid() + ".png";
        }
    }

    public SKBitmap? Load(IDataStorage? dataStorage = null)
    {
        if (string.IsNullOrWhiteSpace(Id))
            return null;

        var data = dataStorage?.GetEntry(Id);

        return data;
    }
}