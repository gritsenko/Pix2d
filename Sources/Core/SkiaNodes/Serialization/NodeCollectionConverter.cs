using Newtonsoft.Json;

namespace SkiaNodes.Serialization;

public class NodeCollectionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(SKNode.NodeCollection));
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is SKNode.NodeCollection collection)
            serializer.Serialize(writer, collection.Where(c => !c.IsAdorner).ToArray());
        else
            writer.WriteNull();
    }

    public override bool CanRead => false;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}