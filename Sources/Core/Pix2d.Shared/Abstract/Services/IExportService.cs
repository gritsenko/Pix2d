#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Defines a service responsible for handling the export of graphical nodes to various formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Gets a read-only list of all registered exporters available in the service.
    /// </summary>
    IReadOnlyList<ExporterInfo> RegisteredExporters { get; }

    /// <summary>
    /// Exports a collection of SKNode objects using a specified exporter and scale.
    /// </summary>
    /// <param name="nodesToRender">The collection of nodes to be rendered for export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export.</param>
    /// <param name="exporterInfo">Information about the exporter to use.</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportNodesAsync(IEnumerable<SKNode> nodesToRender, double scale, ExporterInfo exporterInfo);

    /// <summary>
    /// Exports a collection of SKNode objects to a specific file path using a default or inferred exporter.
    /// </summary>
    /// <param name="fileContentSource">The target file content source to write the exported data to.</param>
    /// <param name="nodesToRender">The collection of nodes to be rendered for export.</param>
    /// <param name="scale">The scaling factor to apply during rendering for export.</param>
    /// <returns>A Task representing the asynchronous export operation.</returns>
    Task ExportNodesToFileAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale);

    /// <summary>
    /// Determines and retrieves the nodes that should be considered for export, potentially based on the current application state and desired scale.
    /// </summary>
    /// <param name="scale">The desired scale of the export, which might influence which nodes are included or how they are prepared.</param>
    /// <returns>An enumerable collection of SKNode objects ready for export rendering.</returns>
    IEnumerable<SKNode> GetNodesToExport(double scale);

    /// <summary>
    /// Registers a new exporter type with the export service.
    /// </summary>
    /// <typeparam name="TExporter">The concrete type of the exporter, which must implement IExporter.</typeparam>
    /// <param name="displayName">An optional human-readable name for the exporter. If null, a default name might be generated.</param>
    /// <param name="createInstanceFunc">A function that provides an instance of the exporter when needed.</param>
    void RegisterExporter<TExporter>(string? displayName, Func<IExporter> createInstanceFunc) where TExporter : IExporter;
}

/// <summary>
/// Represents information about a registered exporter.
/// </summary>
/// <param name="Id">A unique identifier for the exporter.</param>
/// <param name="Name">The display name of the exporter.</param>
/// <param name="ExporterType">The concrete type of the exporter implementation.</param>
/// <param name="CreateInstanceFunc">A function to create a new instance of the exporter.</param>
public record ExporterInfo(string Id, string Name, Type ExporterType, Func<IExporter> CreateInstanceFunc);