#nullable enable
using System.Text.RegularExpressions;

namespace Pix2d.Primitives.Crash;

/// <summary>
/// Collapses run-specific values out of an exception message so that the *same* failure always
/// produces the *same* telemetry signature.
/// <para>
/// Crash/error aggregation keys on the exception message (the appstat signature key is
/// <c>"&lt;kind&gt;|&lt;title&gt;"</c>, and Sentry itself falls back to type + value for the
/// frame-less events that trimmed/AOT Android builds produce). A message that embeds a byte count,
/// a temp path, a user name or a GUID therefore spawns a brand-new single-event group on every
/// occurrence instead of joining the group it belongs to — e.g. one signature per input size for
/// <c>"Size of input data 1306260 is not equal to the size of the bitmap 295200"</c>, and one per
/// typed file name for <c>"Access to the path 'C:\…\Shiney.png' is denied."</c>. Those read as a
/// long tail of count-1 noise and hide how widespread the underlying bug actually is.
/// </para>
/// <para>
/// Only the text that leaves the device as the telemetry *title* is normalized. The concrete values
/// stay fully available for triage: the local crash report (<see cref="CrashReportSummary.Message"/>,
/// shown to and shared by the user) is untouched, and the sinks attach the raw text as the
/// <c>original_message</c> extra alongside the unmodified <c>exception_chain</c>.
/// </para>
/// </summary>
public static partial class TelemetryMessageNormalizer
{
    /// <summary>Signature titles are a grouping key, not a log line — keep them short.</summary>
    private const int MaxLength = 300;

    public static string Normalize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;

        var text = WhitespaceRegex().Replace(message, " ").Trim();

        // Paths first: they carry digits, GUIDs and user names, so running the scalar rules ahead of
        // them would shred a path into <n>/<id> soup instead of recognizing it as one placeholder.
        text = QuotedPathRegex().Replace(text, "$1<path>$1");
        text = WindowsPathRegex().Replace(text, "<path>");
        text = PosixPathRegex().Replace(text, "<path>");

        text = GuidRegex().Replace(text, "<id>");
        text = HexIdRegex().Replace(text, "<id>");
        text = HexNumberRegex().Replace(text, "<n>");
        text = NumberRegex().Replace(text, "<n>");

        return text.Length > MaxLength ? text.Substring(0, MaxLength) + "…" : text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// A quoted path — matched before the bare-path rules because file names legitimately contain
    /// spaces (<c>'C:\Users\james cute\…'</c>, <c>'Coucher de soleil.png'</c>) that a
    /// whitespace-terminated pattern would cut in half. The quotes are kept: they carry the sentence
    /// structure of messages like <c>Could not find a part of the path '…'.</c>
    /// </summary>
    [GeneratedRegex(@"(['""`])(?:[A-Za-z]:[\\/]|\\\\|/)[^'""`\r\n]*\1")]
    private static partial Regex QuotedPathRegex();

    [GeneratedRegex(@"[A-Za-z]:[\\/][^\s'""`\r\n]*")]
    private static partial Regex WindowsPathRegex();

    /// <summary>
    /// Unix/Android absolute path: at least one <c>dir/</c> segment, and not preceded by a word
    /// character so that <c>and/or</c>-style text is left alone.
    /// </summary>
    [GeneratedRegex(@"(?<!\w)/(?:[^\s/'""`\r\n]+/)+[^\s'""`\r\n]*")]
    private static partial Regex PosixPathRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    /// <summary>32 hex chars — <c>Guid.ToString("N")</c>, the form used for crash/session/cache ids.</summary>
    [GeneratedRegex(@"\b[0-9a-fA-F]{32}\b")]
    private static partial Regex HexIdRegex();

    [GeneratedRegex(@"\b0[xX][0-9a-fA-F]+\b")]
    private static partial Regex HexNumberRegex();

    /// <summary>
    /// A standalone number, optionally signed and with decimal/thousand separators. The word
    /// boundary keeps digits that are part of an identifier intact (the <c>32</c> in <c>Int32</c>).
    /// </summary>
    [GeneratedRegex(@"-?\b\d+(?:[.,]\d+)*\b")]
    private static partial Regex NumberRegex();
}
