using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

/// <summary>
/// An <see cref="IFileContentSource"/> over a plain filesystem path, so every write here can take the
/// strong guarantee: stage beside the destination, publish with an atomic rename. See
/// <see cref="StagedWrites.OnDisk"/> for why that matters — an in-place write that dies half-way (full
/// disk, unplugged drive, killed process) leaves an unopenable file where the user's work was.
/// </summary>
public class NetFileSource : IFileContentSource
{
    public string Path { get; }

    public bool Exists => File.Exists(Path);
    public DateTime LastModified => File.GetLastWriteTime(Path);

    public string Extension => System.IO.Path.GetExtension(Path);
    public string Title { get; set; }

    public Task<byte[]> GetContentAsync()
    {
        return Task.FromResult(File.ReadAllBytes(Path));
    }

    /// <inheritdoc />
    public Task<IStagedWrite> OpenStagedWriteAsync()
    {
        PrepareForOverwrite();
        return Task.FromResult(StagedWrites.OnDisk(Path));
    }

    public async Task SaveAsync(Stream sourceStream)
    {
        // ConfigureAwait(false) throughout: callers reach this from the Avalonia dispatcher and some block
        // on the result (the scenario harness does), so capturing the UI context would deadlock.
        await using var staged = await OpenStagedWriteAsync().ConfigureAwait(false);
        await sourceStream.CopyToAsync(staged.Stream).ConfigureAwait(false);
        await staged.CommitAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Text goes through the same staged write as everything else. It used to be a plain
    /// <c>File.WriteAllText</c>, which quietly made the *text* payloads the only unprotected ones — and
    /// those are palettes the user authored and sprite-sheet metadata sidecars, not scratch data.
    /// </summary>
    public Task SaveAsync(string textContent)
        => SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(textContent)));

    /// <summary>
    /// Clears the read-only attribute so an overwrite the user has already asked for can proceed.
    ///
    /// <para>Windows marks files extracted from a <c>.zip</c> read-only, and both paths that reach here —
    /// export to an existing file and Ctrl+S on a PNG-backed project (which writes back to the source
    /// image) — then failed with <c>UnauthorizedAccessException</c> on a file the user had just opened
    /// from an archive: appstat, 3.11.3, `…\Temp\…\VanillaDefault 1.21.7.zip…\ladder.png`. Overwriting is
    /// the whole point of the operation and it is already confirmed upstream (export asks, Ctrl+S means
    /// save), so the read-only flag has nothing left to protect here. A genuine permission problem
    /// (ACLs, another process holding the file) still surfaces as before.</para>
    /// </summary>
    private void PrepareForOverwrite()
    {
        if (!File.Exists(Path))
            return;

        try
        {
            var attributes = File.GetAttributes(Path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(Path, attributes & ~FileAttributes.ReadOnly);
        }
        catch (Exception)
        {
            // Best effort — if the attribute cannot be cleared, let the write itself report the real error.
        }
    }

    public void Delete()
    {
        File.Delete(Path);
    }

    public Task<Stream> OpenRead()
    {
        return Task.FromResult<Stream>(File.OpenRead(Path));
    }

    public NetFileSource(string path)
    {
        Path = path;
        Title = System.IO.Path.GetFileName(Path);
    }
}
