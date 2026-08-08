#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Import.Flow;

/// <summary>
/// Pure (DI-free, side-effect-free) classification of an import file set: file kind and
/// animation grouping by file name. Kept static so it is trivially unit-testable.
/// </summary>
public static class ImportAnalyzer
{
    private static readonly string[] ProjectExtensions = [".pix2d", ".pxm"];
    private static readonly string[] RasterExtensions = [".png", ".jpg", ".jpeg"];

    // Layered source documents: one file decodes straight into layers + frames.
    private static readonly string[] LayeredDocumentExtensions = [".piskel"];

    // base name = file name without trailing separator(s) and digits; num = the trailing digits.
    // e.g. "idle_0001" -> ("idle", 1), "run-2" -> ("run", 2), "frfr0000" -> ("frfr", 0).
    private static readonly Regex FrameNumberRegex =
        new(@"^(?<base>.*?)[ _\-.]*(?<num>\d+)$", RegexOptions.Compiled);

    public static ImportFileKind ClassifyKind(IReadOnlyList<IFileContentSource> files)
    {
        if (files == null || files.Count == 0)
            return ImportFileKind.Unsupported;

        if (files.Any(IsProject))
            return ImportFileKind.Project;

        if (files.All(f => Ext(f) == ".gif"))
            return ImportFileKind.Gif;

        if (files.All(f => LayeredDocumentExtensions.Contains(Ext(f))))
            return ImportFileKind.LayeredDocument;

        if (files.All(f => RasterExtensions.Contains(Ext(f))))
            return ImportFileKind.Raster;

        return ImportFileKind.Unsupported;
    }

    public static bool IsProject(IFileContentSource file) => ProjectExtensions.Contains(Ext(file));

    /// <summary>
    /// Groups files by base name (trailing index stripped). Files within a group are ordered by their
    /// numeric suffix (numeric, not lexicographic) then by original order. Files without a trailing
    /// number each form their own single-file group.
    /// </summary>
    public static IReadOnlyList<ImportGroup> DetectAnimationGroups(IReadOnlyList<IFileContentSource> files)
    {
        var map = new Dictionary<string, List<(int Order, int Seq, IFileContentSource File)>>(StringComparer.OrdinalIgnoreCase);
        var keyOrder = new List<string>();

        var seq = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file.Path) ?? string.Empty;
            var match = FrameNumberRegex.Match(name);

            string key;
            int num;
            if (match.Success && int.TryParse(match.Groups["num"].Value, out var parsed))
            {
                key = match.Groups["base"].Value.Trim();
                if (string.IsNullOrEmpty(key))
                    key = name; // file is purely numeric ("0001") -> keep its own name as the key
                num = parsed;
            }
            else
            {
                key = name;
                num = 0;
            }

            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
                keyOrder.Add(key);
            }

            list.Add((num, seq++, file));
        }

        return keyOrder
            .Select(key => new ImportGroup(
                key,
                map[key].OrderBy(x => x.Order).ThenBy(x => x.Seq).Select(x => x.File).ToList()))
            .ToList();
    }

    private static string Ext(IFileContentSource file) => (file.Extension ?? string.Empty).ToLowerInvariant();
}
