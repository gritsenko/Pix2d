using Avalonia.Platform;

namespace Pix2d.Common;

public class ResourceManager
{
    public static Stream GetAsset(string path) => GetAsset(GetEmbeddedResourceURI(path));
    public static Stream GetAsset(Uri uri) => AssetLoader.Open(uri);
    public static Uri GetEmbeddedResourceURI(string path) => new($"avares://Pix2d.Core/{path.TrimStart('/')}");

}