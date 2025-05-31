using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

public class AvaloniaFolder(IStorageFolder folder) : IWriteDestinationFolder
{
    public string Path => folder.Path.AbsolutePath;

    public IFileContentSource GetFileSource(string name, string extension = "png", bool overWrite = false)
    {
        if (!Directory.Exists(Path))
        {
            Directory.CreateDirectory(Path);
        }

        return new NetFileSource(GetFileName(name, extension.TrimStart('.'), overWrite));
    }

    public async Task<IFileContentSource> GetFileSourceAsync(string name, string extension = "png", bool overwrite = false)
    {
        var file = await folder.CreateFileAsync(GetFileName(name, extension.TrimStart('.')));
        return new AvaloniaFileSource(file);
    }

    public Task<IFileContentSource> GetFileSourceToReadAsync(string name, string extension = "png")
    {
        var path = GetFileName(name, extension.TrimStart('.'), true);
        if (File.Exists(path))
        {
            return Task.FromResult<IFileContentSource>(new NetFileSource(path));
        }
        return Task.FromResult<IFileContentSource>(default);
    }

    public IWriteDestinationFolder GetSubfolder(string folderName)
    {
        throw new NotImplementedException();
    }

    public Task<IWriteDestinationFolder> GetSubfolderAsync(string folderName)
    {
        return Task.FromResult(GetSubfolder(folderName));
    }


    private string GetFileName(string name, string extension, bool overWrite = false)
    {
        var i = 0;
        var filePath = System.IO.Path.Combine(System.IO.Path.GetFileNameWithoutExtension(name) + "." + extension);

        while (!overWrite && File.Exists(filePath)) 
        {
            i++;
            filePath = System.IO.Path.Combine(System.IO.Path.GetFileNameWithoutExtension(name) + "_" + i + "." + extension);
        }

        return filePath;
    }

    public void CopyTemplateFrom(string templatePath)
    {
        CopyFilesRecursively(
            new DirectoryInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templatePath)),
            new DirectoryInfo(Path));
    }

    public Task ClearFolderAsync()
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(Path))
                return;

            foreach (var file in Directory.GetFiles(Path))
            {
                File.Delete(file);
            }

            foreach (var folder in Directory.GetDirectories(Path))
            {
                Directory.Delete(folder, true);
            }

        });
    }

    public Task<IEnumerable<IFileContentSource>> GetFilesAsync(string subfolderPath = default)
    {
        var dirInfo =
            new DirectoryInfo(subfolderPath == default ? Path : System.IO.Path.Combine(this.Path, subfolderPath));

        if (!dirInfo.Exists)
            return Task.FromResult(Enumerable.Empty<IFileContentSource>());

        return Task.FromResult<IEnumerable<IFileContentSource>>(
            dirInfo.GetFiles().Select(x => new NetFileSource(x.FullName))
        );
    }

    public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));

        foreach (var file in source.GetFiles())
        {
            var targetFile = System.IO.Path.Combine(target.FullName, file.Name);
            if (!File.Exists(targetFile))
                file.CopyTo(targetFile);
        }
    }
}