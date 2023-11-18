using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

public class AvaloniaFileSource : IFileContentSource
{
    private readonly IStorageFile _storageFile;
    private MemoryStream _dataStream;
    public string Path => _storageFile.Name;

    public bool Exists => true;
    public DateTime LastModified => DateTime.Now;

    public string Extension => System.IO.Path.GetExtension(_storageFile.Name);
    public string Title { get; set; }

    public async Task SaveAsync(Stream sourceStream)
    {
        if (File.Exists(Path))
            File.Delete(Path);

        await using var fileStream = await _storageFile.OpenWriteAsync();
        await sourceStream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        fileStream.Close();
    }

    public async Task<Stream> OpenWriteAsync()
    {
        return new StreamWrapper(async (ms) =>
        {
            await using var fs = await _storageFile.OpenWriteAsync();
            await ms.CopyToAsync(fs);
            await ms.FlushAsync();
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
        _storageFile = storageFile;
    }


    internal class StreamWrapper : MemoryStream
    {
        private readonly Func<MemoryStream, Task> _onDisposing;

        public StreamWrapper(Func<MemoryStream, Task> onDisposing)
        {
            _onDisposing = onDisposing;
        }
        protected override async void Dispose(bool disposing)
        {
            Seek(0, SeekOrigin.Begin);
            await _onDisposing.Invoke(this);
            base.Dispose(disposing);
        }
    }
}