#nullable enable
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Abstract.Services;

public interface ISettingsService
{ 

    [RequiresUnreferencedCode("JSON serialization uses reflection.")]
    T? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string key);
    bool TryGet<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> (string key, out T? value);
    void Set<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> (string key, T? value);
}

public static class SettingsConstants
{
    public const string FileServiceContexts = "fileServiceContexts";

    public const string ShareToGalleryAuthor = "shareToGalleryAuthor";

    public const string ShareToGalleryEmail = "shareToGalleryEmail";
}