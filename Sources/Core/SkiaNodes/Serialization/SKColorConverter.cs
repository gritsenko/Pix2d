using System;
using Newtonsoft.Json;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public partial class SKColorConverter : JsonConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
            reader.Read();
            var value = reader.ReadAsString();
            reader.Read();
            return SKColor.Parse(value);
        }

    public override bool CanConvert(Type objectType)
    {
            return objectType == typeof(SKColor);
        }

    public override bool CanWrite { get; } = true;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
            writer.WriteStartObject();
            writer.WritePropertyName("skcolor");
            serializer.Serialize(writer, value.ToString(), typeof(SKColor));
            writer.WriteEndObject();
        }
}