using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

public class AvaloniaFolder(IStorageFolder folder) : IWriteDestinationFolder
{
    // TryGetLocalPath gives a real filesystem path; Uri.AbsolutePath does not — for file:///C:/a/My%20Art
    // it yields "/C:/a/My%20Art", which is neither rooted correctly nor unescaped, so every Directory/File
    // call built on it missed the actual folder.
    public string Path => folder.TryGetLocalPath() ?? folder.Path.LocalPath;

    public IFileContentSource GetFileSource(string name, string extension = "png", bool overWrite = false)
    {
        if (!Directory.Exists(Path))
        {
            Directory.CreateDirectory(Path);
        }

        return new NetFileSource(GetFilePath(name, extension.TrimStart('.'), overWrite));
    }

    public async Task<IFileContentSource> GetFileSourceAsync(string name, string extension = "png", bool overwrite = false)
    {
        var file = await folder.CreateFileAsync(GetUniqueFileName(name, extension.TrimStart('.'), overwrite));
        return new AvaloniaFileSource(file!);
    }

    public Task<IFileContentSource> GetFileSourceToReadAsync(string name, string extension = "png")
    {
        var path = GetFilePath(name, extension.TrimStart('.'), true);
        if (File.Exists(path))
        {
            return Task.FromResult<IFileContentSource>(new NetFileSource(path));
        }
        return Task.FromResult<IFileContentSource>(null!);
    }

    public IWriteDestinationFolder GetSubfolder(string folderName)
    {
        throw new NotImplementedException();
    }

    public Task<IWriteDestinationFolder> GetSubfolderAsync(string folderName)
    {
        return Task.FromResult(GetSubfolder(folderName));
    }


    /// <summary>
    /// Full path of the target file inside this folder. Callers that hand the result to a raw File/Directory
    /// API must use this rather than the bare name — a relative name resolves against the process working
    /// directory, which is C:\Windows\System32 when Pix2d is launched via a file association.
    /// </summary>
    private string GetFilePath(string name, string extension, bool overWrite = false)
        => System.IO.Path.Combine(Path, GetUniqueFileName(name, extension, overWrite));

    /// <summary>File name only — this is what IStorageFolder.CreateFileAsync expects.</summary>
    private string GetUniqueFileName(string name, string extension, bool overWrite = false)
    {
        var baseName = System.IO.Path.GetFileNameWithoutExtension(name);
        var fileName = baseName + "." + extension;

        var i = 0;
        // Probe inside this folder. The old code tested a bare name against the working directory, so the
        // collision check practically never matched and existing exports were silently overwritten.
        while (!overWrite && File.Exists(System.IO.Path.Combine(Path, fileName)))
        {
            i++;
            fileName = baseName + "_" + i + "." + extension;
        }

        return fileName;
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

    public Task<IEnumerable<IFileContentSource>> GetFilesAsync(string? subfolderPath = default)
    {
        var dirInfo =
            new DirectoryInfo(subfolderPath == default ? Path : System.IO.Path.Combine(this.Path, subfolderPath!));

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