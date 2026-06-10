#nullable enable
using System.Collections.Generic;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaSharp;

namespace Pix2d.Abstract.Import.Flow;

/// <summary>
/// Immutable input to the import flow.
/// </summary>
/// <param name="Files">The files to import (already materialized as content sources).</param>
/// <param name="DropWorldPosition">
/// World position of the drop, or <c>null</c> when import was invoked from the command / file picker.
/// Used to decide whether a still image lands as a layer of the current sprite or as a new sprite.
/// </param>
/// <param name="FromDrag">True when triggered by drag-and-drop (affects the .pix2d open/import prompt).</param>
public sealed record ImportRequest(
    IReadOnlyList<IFileContentSource> Files,
    SKPoint? DropWorldPosition,
    bool FromDrag);
