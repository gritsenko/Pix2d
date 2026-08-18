#nullable enable
using System.IO;
using System.Threading.Tasks;

namespace Pix2d.Abstract.Platform.FileSystem;

/// <summary>
/// A write in progress whose bytes are not visible at the destination until <see cref="CommitAsync"/>.
///
/// <para>Committing is deliberately an explicit success-path step rather than something that happens on
/// dispose: <c>await using</c> also disposes while an exception is unwinding, so a commit-on-dispose stream
/// would publish a half-written payload — exactly the corruption this exists to prevent. Disposing without
/// committing aborts the write and leaves the existing file untouched.</para>
/// </summary>
public interface IStagedWrite : IAsyncDisposable
{
    /// <summary>The stream to write the payload to. Valid until <see cref="CommitAsync"/> or dispose.</summary>
    Stream Stream { get; }

    /// <summary>Publishes the staged bytes at the destination, replacing whatever was there.</summary>
    Task CommitAsync();
}

/// <summary>
/// The two ways to stage a write, so every <see cref="IFileContentSource"/> implementation shares one
/// mechanism instead of re-deriving it.
///
/// <para>Which one applies is decided by whether a real filesystem path is available — not by which
/// implementation happens to hold the file. A desktop file picked through the storage provider is an
/// ordinary path and gets the strong guarantee; the same class wrapping an Android SAF
/// <c>content://</c> URI or a browser handle has no path and falls back to the weaker one.</para>
/// </summary>
public static class StagedWrites
{
    /// <summary>
    /// Stages into a <c>.tmp</c> sibling and publishes with an atomic rename. The destination is either the
    /// old file or the new one, never a mixture, and nothing is buffered in memory — the payload streams
    /// straight to disk however large it is.
    /// </summary>
    public static IStagedWrite OnDisk(string path) => new DiskStagedWrite(path);

    /// <summary>
    /// Buffers the payload in memory and hands it over complete on commit, for destinations with no rename
    /// primitive. Weaker than <see cref="OnDisk"/>: the handover itself truncates and copies, so a failure
    /// *during the commit* can still leave a partial file. It does guarantee the destination is untouched
    /// while the payload is being produced, which is where the long, failure-prone work happens.
    /// </summary>
    public static IStagedWrite InMemory(Func<Stream, Task> commit) => new BufferedStagedWrite(commit);

    private sealed class DiskStagedWrite : IStagedWrite
    {
        private readonly string _finalPath;
        private readonly string _tempPath;
        private readonly FileStream _stream;
        private bool _committed;

        public DiskStagedWrite(string finalPath)
        {
            _finalPath = finalPath;
            _tempPath = finalPath + ".tmp";
            _stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true);
        }

        public Stream Stream => _stream;

        public async Task CommitAsync()
        {
            await _stream.FlushAsync().ConfigureAwait(false);
            await _stream.DisposeAsync().ConfigureAwait(false);

            // File.Move with overwrite is atomic on the same volume on NTFS, ext4 and APFS — and on Android
            // internal storage / iOS sandbox filesystems, which sit on top of those.
            File.Move(_tempPath, _finalPath, overwrite: true);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            // Idempotent after CommitAsync; on the failure path this is what releases the handle so the
            // abandoned staging file can be deleted.
            await _stream.DisposeAsync().ConfigureAwait(false);

            if (_committed)
                return;

            try
            {
                if (File.Exists(_tempPath))
                    File.Delete(_tempPath);
            }
            catch
            {
                // Best effort — a leftover .tmp is harmless next to a file that was left intact.
            }
        }
    }

    private sealed class BufferedStagedWrite(Func<Stream, Task> commit) : IStagedWrite
    {
        private readonly MemoryStream _buffer = new();

        public Stream Stream => _buffer;

        public async Task CommitAsync()
        {
            _buffer.Seek(0, SeekOrigin.Begin);
            await commit(_buffer).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _buffer.Dispose();
            return default;
        }
    }
}
