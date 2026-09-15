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
    ///
    /// <para>Falls back to <see cref="InMemory"/> when the staging sibling cannot be created at all. The
    /// sibling's name is always <i>longer</i> than the destination's (<c>&lt;final&gt;.&lt;guid&gt;.tmp</c>),
    /// so a destination whose own name sits near the filesystem's 255-character component limit has no
    /// legal staging name — and a security filter or a sync client can refuse an unknown <c>.tmp</c> in a
    /// watched folder while allowing the real file (<c>FileNotFoundException</c> on
    /// <c>…\Documents\….pix2d.tmp</c>, 3.13.0, from <c>File.SaveAs</c>). Refusing here loses the save
    /// outright; the fallback keeps the weaker "destination untouched until the payload is complete"
    /// guarantee instead, which is the one every path-less destination already lives with.</para>
    /// </summary>
    public static IStagedWrite OnDisk(string path)
    {
        try
        {
            return new DiskStagedWrite(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A destination that is itself unwritable still fails — one step later, when the buffered
            // payload is handed over, and by then the caller has produced it exactly once either way.
            Logger.Log($"Staged write fell back to a buffered write for '{path}': {e.Message}");
            return InMemory(payload => WriteDirectlyAsync(path, payload));
        }
    }

    /// <summary>
    /// The fallback's commit: truncate the destination and copy the finished payload into it.
    /// <see cref="FileMode.Create"/> rather than an append-capable mode — writing a shorter archive over a
    /// longer one without truncating leaves the old tail in place, which is one of the two ways a
    /// <c>.pix2d</c> used to become unopenable.
    /// </summary>
    private static async Task WriteDirectlyAsync(string path, Stream payload)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true);

        await payload.CopyToAsync(destination).ConfigureAwait(false);
        await destination.FlushAsync().ConfigureAwait(false);
    }

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

            // The staging file is named per *write*, not per destination. It used to be a fixed
            // "<final>.tmp" sibling opened with FileShare.None, so two saves of the same project
            // overlapping — a close-triggered save landing on top of an autosave, which the op log shows
            // verbatim — made the second one fail to even open it: IOException "the process cannot
            // access the file … .pix2d.tmp because it is being used by another process", and because the
            // save task was never awaited it came back as an unobserved exception from the finalizer,
            // i.e. fatal. Two concurrent writers now stage independently and the later rename wins,
            // which is the same outcome the fixed name was trying to produce anyway.
            _tempPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";

            // The destination folder can be gone by the time a save runs: a project is opened once and
            // saved for hours, and the user reorganises the folder it came from in the meantime
            // (DirectoryNotFoundException on "…\\Downloads\\movin\\characters\\…\\onekin sprites.pix2d.tmp",
            // 3.13.0, after earlier saves in the same session had succeeded). Asking to save to a path
            // is asking for the path to exist, and refusing here loses the work the save was meant to
            // preserve. A destination that genuinely cannot be created — an unplugged drive, a denied
            // parent — still fails exactly as before, one line later.
            var directory = Path.GetDirectoryName(_tempPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true);
        }

        /// <summary>
        /// How many times the rename is attempted, and the base backoff between attempts (it grows
        /// linearly, so the whole budget is ~360 ms). On Windows the destination of an export is
        /// routinely opened for a moment by something that is not the user — a virus scanner reading
        /// what was just written, the search indexer, a OneDrive/Dropbox client picking up the change —
        /// and the rename then fails with <c>ERROR_ACCESS_DENIED</c>/<c>ERROR_SHARING_VIOLATION</c>
        /// (<c>UnauthorizedAccessException</c> was the 3rd-largest signature in appstat: 17 events over
        /// 6 users, all from <c>PngImageExporter</c>). Those handles are held for milliseconds, so one
        /// retry usually settles it, while a genuine permission problem still surfaces to the caller —
        /// the budget is deliberately short enough not to stall a user-visible export.
        /// </summary>
        private const int PublishAttempts = 4;
        private const int PublishRetryDelayMs = 60;

        public Stream Stream => _stream;

        public async Task CommitAsync()
        {
            await _stream.FlushAsync().ConfigureAwait(false);
            await _stream.DisposeAsync().ConfigureAwait(false);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    // File.Move with overwrite is atomic on the same volume on NTFS, ext4 and APFS — and on
                    // Android internal storage / iOS sandbox filesystems, which sit on top of those. The
                    // retry keeps that guarantee: a failed attempt publishes nothing, so there is no
                    // half-written destination to recover from, and no copy-fallback that would quietly
                    // downgrade the atomicity this class exists to provide.
                    File.Move(_tempPath, _finalPath, overwrite: true);
                    break;
                }
                catch (Exception e) when (attempt < PublishAttempts
                                          && e is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(PublishRetryDelayMs * attempt).ConfigureAwait(false);
                }
            }

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
