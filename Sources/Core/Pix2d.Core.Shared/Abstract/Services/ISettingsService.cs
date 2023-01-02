namespace Pix2d.Abstract
{
    public interface ISettingsService
    {
        string GetRaw(string key);
        T Get<T> (string key);
        bool TryGet<T> (string key, out T value);
        void Set<T> (string key, T value);
    }
}