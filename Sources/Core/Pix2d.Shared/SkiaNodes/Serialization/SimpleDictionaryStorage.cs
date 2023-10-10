using System.Collections.Generic;
using SkiaSharp;

namespace SkiaNodes.Serialization
{
    public class SimpleDictionaryStorage : IDataStorage
    {
        private readonly IDictionary<string, SKBitmap> _data;

        public SimpleDictionaryStorage(IDictionary<string, SKBitmap> data = null)
        {
            _data = data ?? new Dictionary<string, SKBitmap>();
        }

        public void SetEntry(string id, SKBitmap data)
        {
            _data[id] = data;
        }

        public SKBitmap GetEntry(string id)
        {
            return _data.TryGetValue(id, out SKBitmap data) ? data : null;
        }

        public IDictionary<string, SKBitmap> GetDataEnries()
        {
            return _data;
        }

    }
}