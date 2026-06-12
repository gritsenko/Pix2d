using Pix2d.Abstract.Services;

namespace Pix2d.Abstract.Operations;

public interface ICacheableOperation
{
    void EvictToDisk(IOperationDiskCacheService cache);

    void RestoreFromDisk(IOperationDiskCacheService cache);

    /// <summary>
    /// Deletes this operation's disk-cached payload(s). Called when the operation leaves the
    /// undo/redo history for good (overflow eviction, redo clear, history/tab removal) so the
    /// shared temp cache folder does not grow without bound while several projects keep it alive.
    /// Safe to call whether or not the payload is currently evicted.
    /// </summary>
    void ClearDiskCache(IOperationDiskCacheService cache);
}
