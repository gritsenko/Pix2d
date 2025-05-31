#nullable enable
using SkiaNodes;

namespace Pix2d.Abstract.Export;

/// <summary>
/// Defines the basic contract for an exporter that can take a collection of SKNodes
/// and render them to some output format.
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Gets the display title of the exporter.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Exports a collection of SKNode objects. The specific output mechanism
    /// (e.g., showing a save dialog, writing to a default location) is up to the implementation.
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export (default is 1).</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1);

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
/// Extends <see cref="IExporter"/> to define an exporter that prompts the user to select a folder
/// and exports content into that folder.
/// </summary>
public interface IFolderPickerExporter : IExporter
{
    /// <summary>
    /// Exports a collection of SKNode objects into a folder chosen by the user.
    /// This typically involves prompting the user with a folder picker dialog.
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export (default is 1).</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportToFolderAsync(IEnumerable<SKNode> nodes, double scale = 1);

}

/// <summary>
/// Extends <see cref="IExporter"/> to define an exporter that prompts the user to select a file path
/// and exports content to that file.
/// </summary>
public interface IFilePickerExporter : IExporter
{
    /// <summary>
    /// Exports a collection of SKNode objects to a file path chosen by the user.
    /// This typically involves prompting the user with a file save dialog.
    /// </summary>
    /// <param name="nodes">The collection of nodes to export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export (default is 1).</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1);
}