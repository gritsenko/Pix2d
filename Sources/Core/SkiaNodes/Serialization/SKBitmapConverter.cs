using System;
using Newtonsoft.Json;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public partial class SKBitmapConverter : JsonConverter
{
    public IDataStorage? DataStorage { get; set; }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var bmdef = serializer.Deserialize<SKBitmapRef>(reader);
        return bmdef?.Id != null && DataStorage != null ? DataStorage.GetEntry(bmdef.Id) : null;
    }

    public override bool CanConvert(Type objectType)
    {
            return objectType == typeof(SKBitmap) || objectType.IsSubclassOf(typeof(SKBitmap));
        }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
            if (value is SKBitmap bitmap)
            {
                var bmdef = new SKBitmapRef(bitmap, DataStorage);
                var th = serializer.TypeNameHandling;
                serializer.TypeNameHandling = TypeNameHandling.All;
                serializer.Serialize(writer, bmdef, typeof(SKBitmapRef));
                serializer.TypeNameHandling = th;
            }
        }
}