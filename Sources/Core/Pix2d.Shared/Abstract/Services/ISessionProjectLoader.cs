using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Services;

public interface ISessionProjectLoader
{
    Task OpenProjectFromSessionAsync(IFileContentSource sessionFile);
}