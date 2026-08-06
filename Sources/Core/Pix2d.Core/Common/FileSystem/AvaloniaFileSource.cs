using System.IO;
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
        //if (File.Exists(Path))
        //    File.Delete(Path);

        await using var fileStream = await _storageFile.OpenWriteAsync();
        await sourceStream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        fileStream.Close();
        await sourceStream.DisposeAsync();
    }

    public async Task<Stream> OpenWriteAsync()
    {
        return new StreamWrapper(async (ms) =>
        {
            await using var fs = await _storageFile.OpenWriteAsync();
            await ms.CopyToAsync(fs);
            ms.Flush();
            fs.Close();
        });
    }

    public void Delete()
    {
        File.Delete(Path);
    }

    public void Save(string textContent)
    {
        File.WriteAllText(Path, textContent);
    }

    public Task SaveAsync(string textContent)
    {
        return Task.Run(() => { Save(textContent); });
    }

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


    /// <summary>
    /// Buffers the write in memory and flushes it to the picked file when the stream is disposed.
    ///
    /// <para>The flush used to run from an <c>async void</c> <see cref="Dispose(bool)"/>, which had two
    /// consequences: the caller's <c>await using</c> returned before the file had been written, and any
    /// write failure — a denied path, a full disk — escaped the awaiting caller entirely and arrived as an
    /// unhandled exception on the dispatcher, i.e. a crash instead of an export error the UI could report
    /// (appstat: <c>Access to the path 'C:\Windows\System32\untitled.png' is denied</c>). Every call site
    /// uses <c>await using</c>, so overriding <see cref="DisposeAsync"/> is what actually runs.</para>
    /// </summary>
    internal class StreamWrapper : MemoryStream
    {
        private readonly Func<MemoryStream, Task> _onDisposing;
        private bool _flushed;

        public StreamWrapper(Func<MemoryStream, Task> onDisposing)
        {
            _onDisposing = onDisposing;
        }

        public override async ValueTask DisposeAsync()
        {
            await FlushToTargetAsync();
            await base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            // Synchronous fallback for a plain `using`. Task.Run keeps the continuations off the caller's
            // SynchronizationContext — blocking the UI thread on a continuation posted back to it would
            // deadlock. base.DisposeAsync() also lands here, hence the _flushed guard.
            if (disposing && !_flushed)
                Task.Run(FlushToTargetAsync).GetAwaiter().GetResult();

            base.Dispose(disposing);
        }

        private async Task FlushToTargetAsync()
        {
            if (_flushed)
                return;

            _flushed = true;
            Seek(0, SeekOrigin.Begin);
            await _onDisposing.Invoke(this);
        }
    }
}