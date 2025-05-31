#nullable enable
namespace Pix2d.Abstract.Services;

public interface ISettingsService
{ 
    T? Get<T> (string key);
    bool TryGet<T> (string key, out T? value);
    void Set<T> (string key, T? value);
}

public static class SettingsConstants
{
    public const string FileServiceContexts = "fileServiceContexts";

    public const string ShareToGalleryAuthor = "shareToGalleryAuthor";

    public const string ShareToGalleryEmail = "shareToGalleryEmail";
}