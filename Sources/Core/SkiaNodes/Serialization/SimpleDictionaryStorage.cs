using SkiaSharp;

namespace SkiaNodes.Serialization;

public class SimpleDictionaryStorage(IDictionary<string, SKBitmap>? data = null) : IDataStorage
{
    private readonly IDictionary<string, SKBitmap> _data = data ?? new Dictionary<string, SKBitmap>();

    public void SetEntry(string id, SKBitmap data)
    {
        _data[id] = data;
    }

    public SKBitmap? GetEntry(string id)
    {
        return _data.TryGetValue(id, out var data) ? data : null;
    }

    public string GetOrCreateId(SKBitmap data)
    {
        foreach (var kvp in _data)
        {
            if (ReferenceEquals(kvp.Value, data))
                return kvp.Key;
        }
        var newId = Guid.NewGuid() + ".png";
        _data[newId] = data;
        return newId;
    }

    public IDictionary<string, SKBitmap> GetDataEntries()
    {
        return _data;
    }
}