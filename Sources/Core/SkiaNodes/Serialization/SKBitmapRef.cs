using System;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public class SKBitmapRef
{
    public string Id { get; set; }

    public SKBitmapRef()
    {
        }

    public SKBitmapRef(SKBitmap sourceObject, IDataStorage dataStorage)
    {
            Id = Guid.NewGuid() + ".png";

            ///todo: slow - make raw bytes instaed of png
            dataStorage.SetEntry(Id, sourceObject);
        }

    public SKBitmap Load(IDataStorage dataStorage = null)
    {
            if (string.IsNullOrWhiteSpace(Id))
                return null;

            var data = dataStorage?.GetEntry(Id);

            return data;
        }
}