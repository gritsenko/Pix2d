using System;
using Pix2d.Abstract.Services;

namespace Pix2d.Operations;

/// <summary>
/// Helper class for managing cached payloads with disk eviction capabilities.
/// </summary>
public class CachedPayload<T>
{
    private T? _value;
    private readonly string _cacheKey;
    private bool _isEvicted;

    public CachedPayload(T initialValue, string cacheKeyPrefix = "payload")
    {
        _value = initialValue ?? throw new ArgumentNullException(nameof(initialValue));
        _cacheKey = $"{cacheKeyPrefix}_{Guid.NewGuid():N}";
        _isEvicted = false;
    }

    /// <summary>
    /// Gets the current value, restoring from disk if necessary.
    /// </summary>
    public T GetValue()
    {
        if (_isEvicted)
        {
            throw new InvalidOperationException("Payload has been evicted and cannot be restored without a cache service. Use RestoreFromDisk() first.");
        }
        return _value!;
    }

    /// <summary>
    /// Sets a new value, marking as not evicted.
    /// </summary>
    public void SetValue(T value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _isEvicted = false;
    }

    /// <summary>
    /// Evicts the payload to disk using custom serialization, clearing the in-memory value.
    /// </summary>
    public void EvictToDisk(IOperationDiskCacheService cache, Func<T, byte[]> serialize)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));
        if (serialize == null) throw new ArgumentNullException(nameof(serialize));

        if (_isEvicted) return;

        byte[] data = serialize(_value!);
        cache.SaveData(_cacheKey, data);
        
        // Help GC handle references if not unmanaged like byte[]
        if (_value is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        _value = default;
        _isEvicted = true;
    }

    /// <summary>
    /// Restores the payload from disk.
    /// </summary>
    public void RestoreFromDisk(IOperationDiskCacheService cache, Func<byte[], T> deserialize)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));
        if (deserialize == null) throw new ArgumentNullException(nameof(deserialize));

        if (!_isEvicted) return;

        var bytes = cache.LoadData(_cacheKey);
        _value = deserialize(bytes);
        _isEvicted = false;
    }

    /// <summary>
    /// Clears the cache entry from disk (e.g. for complete deletion).
    /// </summary>
    public void ClearDiskCache(IOperationDiskCacheService cache)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));
        cache.DeleteData(_cacheKey);
    }

    public bool IsEvicted => _isEvicted;
}
