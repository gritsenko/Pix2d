using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Project;

public class ProjectsCollection
{
    public ProjectsCollection(IEnumerable<IFileContentSource> mrus, IEnumerable<IFileContentSource> ownProjects)
    {
        RecentProjects = mrus.Where(x => x.Exists).Select(x => new PreloadedProject(x)).ToArray();
        OwnProjects = ownProjects.Select(x => new PreloadedProject(x)).ToArray();
    }
    
    public PreloadedProject[] RecentProjects { get; }
    public PreloadedProject[] OwnProjects { get; }

    public IEnumerable<PreloadedProject> FilteredOwnProjects =>
        OwnProjects.Where(x => RecentProjects.All(p => p.Path != x.Path));

    public IEnumerable<PreloadedProject> AllProjects => FilteredOwnProjects.Concat(RecentProjects);
}

public class PreloadedProject
{
    private readonly IFileContentSource _file;
    private Task<SKBitmap?> _previewTask;

    public PreloadedProject(IFileContentSource file)
    {
        _file = file;
    }

    private static readonly Regex NameTrimRegex = new Regex("\\.pix2d$");
    public string Name => NameTrimRegex.Replace(_file.Title, "");
    public string Path => _file.Path;

    public IFileContentSource File => _file;

    public Task<SKNode> LoadAsync()
    {
        return ProjectUnpacker.LoadProjectScene(_file);
    }

    public Task<SKBitmap?> LoadPreviewAsync()
    {
        _previewTask ??= ProjectUnpacker.LoadPreview(_file);
        return _previewTask;
    }
}