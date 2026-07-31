#nullable enable
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;

namespace Pix2d.Abstract.Export;

/// <summary>
/// Identity of an exporter: what it's called and what it produces. Exporting itself lives in the capability
/// interfaces below — <see cref="IStreamExporter"/>, <see cref="IFilePickerExporter"/>,
/// <see cref="IBatchExporter"/> — each of which says how this exporter can *write*. Deciding *where* is
/// <c>IExportService</c>'s job, not the exporter's, which is what lets one exporter serve both a single Save
/// dialog and an N-artboard batch into a folder.
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Gets the display title of the exporter.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Gets an array of file extensions supported by this exporter (e.g., ".png", ".jpg").
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Gets the MIME type of the output format (e.g., "image/png").
    /// </summary>
    string MimeType { get; }
}

/// <summary>
/// Extends <see cref="IExporter"/> to define an exporter that can output the exported data as a Stream.
/// </summary>
public interface IStreamExporter : IExporter
{
    /// <summary>
    /// Exports a collection of SKNode objects and returns the result as a Stream.
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export (default is 1).</param>
    /// <returns>A Task that resolves to a Stream containing the exported data.</returns>
    Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodes, double scale = 1);

}

/// <summary>
/// Extends <see cref="IExporter"/> to define an exporter that prompts the user to select a file path
/// and exports content to that file.
/// <para>
/// Implementing this interface is what makes an exporter eligible for the single-file destination:
/// <c>IExportService</c> routes a one-artboard export through <see cref="ExportToFileAsync"/> (a Save
/// dialog) and everything else through a folder. An exporter that cannot meaningfully produce one file
/// per user interaction (a frame sequence) must therefore *not* implement this.
/// </para>
/// </summary>
public interface IFilePickerExporter : IExporter
{
    /// <summary>
    /// Exports a collection of SKNode objects to a file path chosen by the user.
    /// This typically involves prompting the user with a file save dialog.
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export (default is 1).</param>
    /// <param name="defaultFileName">Suggested base file name (no extension) for the save dialog.</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1, string? defaultFileName = null);
}

/// <summary>
/// Extends <see cref="IExporter"/> to define an exporter that writes into a destination folder supplied
/// by the caller rather than one it asks for itself.
/// <para>
/// This is the batch-export primitive. <see cref="IFilePickerExporter"/> owns its own save dialog, so it
/// can only ever produce one artwork per user interaction; an <c>IBatchExporter</c> receives the
/// destination instead, letting <c>IExportService</c> pick a folder once and drive N artboards through
/// the same exporter instance. Implement it whenever one artwork does not map to exactly one stream —
/// a sprite sheet (PNG + JSON sidecar) or a PNG frame sequence. Exporters that *are* a single stream
/// need nothing: <c>IExportService</c> writes their <see cref="IStreamExporter"/> output itself.
/// </para>
/// </summary>
public interface IBatchExporter : IExporter
{
    /// <summary>
    /// True when one artwork yields several files whose names this exporter controls (a frame sequence),
    /// so a batch export must isolate each artboard in its own subfolder to avoid collisions. False when
    /// every produced name is already derived from the supplied base name (sheet PNG + sidecar), in which
    /// case all artboards can share one folder.
    /// </summary>
    bool NeedsOwnFolderPerItem { get; }

    /// <summary>
    /// Writes one artwork into <paramref name="folder"/>. Every produced file name must be derived from
    /// <paramref name="baseName"/> unless <see cref="NeedsOwnFolderPerItem"/> is true (in which case the
    /// caller has already isolated this artwork in its own folder).
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export.</param>
    /// <param name="folder">Destination folder, already chosen and confirmed by the caller.</param>
    /// <param name="baseName">Base file name (no extension), already sanitized for the filesystem.</param>
    Task ExportToFolderAsync(IEnumerable<SKNode> nodes, double scale, IWriteDestinationFolder folder, string baseName);
}
