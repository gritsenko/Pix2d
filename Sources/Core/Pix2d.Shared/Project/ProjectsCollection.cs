#nullable enable
using System.Text.RegularExpressions;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Project;

public class ProjectsCollection
{
    public ProjectsCollection(IEnumerable<IFileContentSource> mrus)
    {
        RecentProjects = mrus.Where(x => x.Exists).Select(x => new PreloadedProject(x)).ToArray();
    }
    
    public PreloadedProject[] RecentProjects { get; }
}

public class PreloadedProject(IFileContentSource file)
{
    private static readonly Regex NameTrimRegex = new Regex("\\.pix2d$");
    public string Name => NameTrimRegex.Replace(file.Title, "");
    public string Path => file.Path;

    public IFileContentSource File => file;

    public Task<SKNode> LoadAsync()
    {
        return ProjectUnpacker.LoadProjectScene(file);
    }

    public async Task<SKBitmap?> LoadPreviewAsync()
    {
        if (file.Extension.ToLower().Equals(".pix2d"))
        {
            return await ProjectUnpacker.LoadPreview(file);
        }

        if (file.Extension.ToLower().Equals(".png") 
            || file.Extension.ToLower().Equals(".jpg")
            || file.Extension.ToLower().Equals(".jpeg"))
        {
            await using var imageDataStream = await file.OpenRead();
            var data = imageDataStream.ToSKBitmap();
            return data;
        }

        return null!;
    }
}