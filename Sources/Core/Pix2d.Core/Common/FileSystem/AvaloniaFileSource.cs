using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

#nullable enable

public class AvaloniaFileSource : IFileContentSource
{
    private readonly IStorageFile _storageFile;
    private MemoryStream _dataStream = null!;
    public string Path { get; }

    public bool Exists => true;
    public DateTime LastModified => DateTime.Now;

    public string Extension => System.IO.Path.GetExtension(_storageFile.Name);
    public string Title { get; set; }

    public async Task SaveAsync(Stream sourceStream)
    {
        if (HasLocalPath)
        {
            await using var staged = StagedWrites.OnDisk(Path);
            await sourceStream.CopyToAsync(staged.Stream);
            await staged.CommitAsync();
        }
        else
        {
            await WriteThroughStorageAsync(sourceStream);
        }

        await sourceStream.DisposeAsync();
    }

    /// <inheritdoc />
    public Task<IStagedWrite> OpenStagedWriteAsync()
        => Task.FromResult(HasLocalPath
            ? StagedWrites.OnDisk(Path)
            : StagedWrites.InMemory(WriteThroughStorageAsync));

    /// <summary>
    /// A file picked through Avalonia's storage provider is an ordinary filesystem path on desktop and has
    /// none on Android SAF / in the browser. That distinction — not the class — decides which staging
    /// guarantee is available, so desktop saves (including every "Save as", which is where the project's
    /// file source comes from) get the atomic-rename path rather than the weaker buffered one.
    /// </summary>
    private bool HasLocalPath => !string.IsNullOrEmpty(Path);

    /// <summary>
    /// Last-resort write for a destination with no path: truncate the storage item and copy. The explicit
    /// truncation is not redundant — writing a shorter archive over a longer one without it leaves the old
    /// tail in place, and a zip reader scanning backwards then finds the *previous* end-of-central-directory
    /// record ("Number of entries expected in End Of Central Directory does not correspond to number of
    /// entries in Central Directory" — the state <c>TestImages/ptvRightTemplate.pix2d</c> is in).
    /// </summary>
    private async Task WriteThroughStorageAsync(Stream sourceStream)
    {
        await using var fileStream = await _storageFile.OpenWriteAsync();

        if (fileStream.CanSeek && fileStream.Length > 0)
        {
            fileStream.SetLength(0);
            fileStream.Position = 0;
        }

        await sourceStream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        fileStream.Close();
    }

    /// <summary>
    /// <see cref="Path"/> is empty whenever the picked file has no filesystem path — an Android SAF
    /// <c>content://</c> URI, a browser file handle. Everything that touches the file must therefore go
    /// through <see cref="IStorageFile"/>, never through <see cref="File"/> and a raw path: the text
    /// overload used to call <c>File.WriteAllText(Path, …)</c>, which is what broke sprite-sheet export on
    /// Android with <c>ArgumentException: The value cannot be an empty string (Parameter 'path')</c> — the
    /// sheet PNG (a stream write) landed, then its metadata sidecar threw.
    /// </summary>
    public void Delete()
    {
        if (!string.IsNullOrEmpty(Path))
        {
            File.Delete(Path);
            return;
        }

        // Task.Run keeps the continuation off the caller's SynchronizationContext — blocking the UI thread
        // on one posted back to it would deadlock.
        Task.Run(async () => await _storageFile.DeleteAsync()).GetAwaiter().GetResult();
    }

    public Task SaveAsync(string textContent)
        => SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(textContent)));

    public async Task<Stream> OpenRead()
    {
        await using var fs = await _storageFile.OpenReadAsync();
        _dataStream = new MemoryStream();
        await fs.CopyToAsync(_dataStream);
        await fs.FlushAsync();

        _dataStream.Seek(0, SeekOrigin.Begin);
        return _dataStream;
    }

    public AvaloniaFileSource(IStorageFile storageFile)
    {
        Title = storageFile.Name;
        Path = storageFile.TryGetLocalPath() ?? "";
        _storageFile = storageFile;
    }
}
