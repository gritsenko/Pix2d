using System;
using System.IO;
using System.Threading.Tasks;

namespace Pix2d.Abstract.Platform.FileSystem;

public interface IFileContentSource
{
    string Path { get; }
    bool Exists { get; }
    DateTime LastModified { get; }

    /// <summary>
    /// file extension in .xxx format (include lead dot)
    /// </summary>
    string Extension { get; }

    string Title { get; set; }

    Task SaveAsync(Stream sourceStream);

//        void SaveAsync(string textContent);

    Task SaveAsync(string textContent);
    Task<Stream> OpenRead();

    /// <summary>
    /// Opens a write that this file does not see until <see cref="IStagedWrite.CommitAsync"/>. Use it for a
    /// payload produced incrementally (a zip written entry by entry); a payload you already hold complete
    /// goes to <see cref="SaveAsync(Stream)"/> instead, which gives the same guarantee.
    ///
    /// <para>There is deliberately no way to obtain a plain writable stream straight onto the destination:
    /// that truncates the file before the new content is known to exist, and a failure part-way through
    /// then leaves nothing recoverable. Implementations stage as well as their platform allows — beside the
    /// file and published with an atomic rename where there is a real path, in memory where there is not.</para>
    /// </summary>
    Task<IStagedWrite> OpenStagedWriteAsync();

    void Delete();
}