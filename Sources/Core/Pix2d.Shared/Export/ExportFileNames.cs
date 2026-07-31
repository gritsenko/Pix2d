#nullable enable
using System.IO;
using System.Text;

namespace Pix2d.Export;

/// <summary>
/// File-name hygiene for export. Artboard names are free-form user text ("hero / idle", "boss:phase 2"),
/// but they end up as file names — both as a save-dialog suggestion and, in a batch export, as the actual
/// name written into the picked folder. Every name that reaches the filesystem goes through here.
/// </summary>
public static class ExportFileNames
{
    /// <summary>Fallback when a name sanitizes down to nothing.</summary>
    public const string Fallback = "untitled";

    private const int MaxLength = 96;

    /// <summary>
    /// Turns arbitrary user text into a safe base file name: drops path separators and characters the
    /// platform rejects, collapses whitespace runs, trims leading/trailing dots and spaces (Windows
    /// silently strips trailing ones, which would break a later name match), and caps the length so a
    /// long artboard name plus a suffix can't overflow the platform's path limit.
    /// Returns an empty string when nothing usable is left — callers decide the fallback.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        var lastWasSpace = false;

        foreach (var c in name)
        {
            if (char.IsControl(c) || Array.IndexOf(invalid, c) >= 0)
                continue;

            if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0 && !lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            sb.Append(c);
            lastWasSpace = false;
        }

        var result = sb.ToString().Trim().TrimEnd('.', ' ').TrimStart('.', ' ');

        if (result.Length > MaxLength)
            result = result[..MaxLength].TrimEnd('.', ' ');

        return result;
    }

    /// <summary>Sanitizes and substitutes <see cref="Fallback"/> when nothing usable remains.</summary>
    public static string SanitizeOrFallback(string? name)
    {
        var sanitized = Sanitize(name);
        return sanitized.Length > 0 ? sanitized : Fallback;
    }
}
