using System.Collections.Generic;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Import.Flow;

/// <summary>
/// A set of files that belong together (e.g. all frames of one animation, ordered by frame index).
/// </summary>
public sealed record ImportGroup(string Name, IReadOnlyList<IFileContentSource> OrderedFiles);

/// <summary>
/// The resolved import plan: which mode to run and the file groups it operates on.
/// </summary>
public sealed record ImportPlan(ImportMode Mode, IReadOnlyList<ImportGroup> Groups);
