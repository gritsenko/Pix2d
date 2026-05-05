using Pix2d.Abstract.Services;

namespace Pix2d.Abstract.Operations;

public interface ICacheableOperation
{
    void EvictToDisk(IOperationDiskCacheService cache);
    
    void RestoreFromDisk(IOperationDiskCacheService cache);
}
