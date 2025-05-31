using Newtonsoft.Json;

namespace SkiaNodes.Serialization;

public class NodeCollectionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(SKNode.NodeCollection));
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, ((SKNode.NodeCollection)value).Where(c => !c.IsAdorner).ToArray());
    }

    public override bool CanRead => false;

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}