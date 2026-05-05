namespace Pix2d.Abstract.Services;

public interface IOperationDiskCacheService
{
    void SaveData(string key, byte[] data);
    byte[] LoadData(string key);
    void DeleteData(string key);
    void ClearAll();
}
