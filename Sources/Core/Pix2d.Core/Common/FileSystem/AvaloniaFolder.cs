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

    /// <summary>
    /// Resolves the destination for one exported item.
    ///
    /// <para>Where the picked folder is an ordinary directory this returns a <see cref="NetFileSource"/> for
    /// the target path and creates nothing: <c>CreateFileAsync</c> truncates an existing file the moment the
    /// destination is *resolved*, which is before the exporter has produced a single byte — so an export
    /// that then failed had already destroyed the file it was replacing. The write itself is staged, so the
    /// old content now survives right up to the atomic rename. Only a folder with no usable filesystem path
    /// (Android SAF, browser) still goes through the storage provider.</para>
    /// </summary>
    public async Task<IFileContentSource> GetFileSourceAsync(string name, string extension = "png", bool overwrite = false)
    {
        var ext = extension.TrimStart('.');

        // Directory.Exists is the only honest test here. A SAF folder's Path falls back to the content:// URI's
        // LocalPath, which is a non-empty string that is not a filesystem path at all — testing for
        // "non-empty" would send Android writes to a bogus location.
        var localPath = folder.TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath) && Directory.Exists(localPath))
            return GetFileSource(name, ext, overwrite);

        var file = await folder.CreateFileAsync(GetUniqueFileName(name, ext, overwrite));
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

    // Batch export needs real subfolders (one per artboard for a frame sequence), so these are no longer
    // NotImplementedException stubs. Prefer the async form: it goes through the storage provider and so also
    // works where the picked folder has no usable filesystem path (Android SAF, browser).
    public IWriteDestinationFolder GetSubfolder(string folderName)
    {
        var path = System.IO.Path.Combine(Path, folderName);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return new NetFolder(path);
    }

    public async Task<IWriteDestinationFolder> GetSubfolderAsync(string folderName)
    {
        await foreach (var item in folder.GetItemsAsync())
        {
            if (item is IStorageFolder existing && string.Equals(item.Name, folderName, StringComparison.OrdinalIgnoreCase))
                return new AvaloniaFolder(existing);
        }

        var created = await folder.CreateFolderAsync(folderName);
        return created != null ? new AvaloniaFolder(created) : GetSubfolder(folderName);
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