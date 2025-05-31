using System;
using Newtonsoft.Json;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public partial class SKBitmapConverter : JsonConverter
{
    public IDataStorage DataStorage { get; set; }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
            reader.Read();
            reader.Read();
            reader.Read();
            var id = reader.ReadAsString();
            reader.Read();
            return DataStorage.GetEntry(id);
        }

    public override bool CanConvert(Type objectType)
    {
            return objectType == typeof(SKBitmap) || objectType.IsSubclassOf(typeof(SKBitmap));
        }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
            //            writer.WriteStartObject();

            var bmdef = new SKBitmapRef((SKBitmap)value, DataStorage);
            var th = serializer.TypeNameHandling;
            serializer.TypeNameHandling = TypeNameHandling.All;
            serializer.Serialize(writer, bmdef, typeof(SKBitmapRef));
            serializer.TypeNameHandling = th;


            //            writer.WriteEndObject();
        }
}