#nullable enable
using System.Text.RegularExpressions;

namespace Pix2d.Primitives.Crash;

/// <summary>
/// Derives a stable grouping key for a crash that the app only learns about <b>after the fact</b>,
/// from the OS exit record of the previous process (Android <c>ApplicationExitInfo</c>).
/// <para>
/// Without this every recovered crash reports the same generic sentence ("the previous process
/// terminated abnormally"), so a hundred unrelated native faults collapse into one useless
/// telemetry signature. The job here is the opposite of
/// <see cref="TelemetryMessageNormalizer"/>: that one strips run-specific noise out of a message,
/// this one *builds* a title out of the few parts of a tombstone that are actually stable.
/// </para>
/// <para><b>What is stable and what is not.</b> Stable: the signal (taken from
/// <see cref="ProcessExitDetails.Status"/> — a number the OS reports structurally, not text that
/// varies by OEM), the <em>basename</em> of the faulting library, and the exported symbol when the
/// linker could resolve one. Not stable, and therefore never part of the key: program counters and
/// fault addresses, the <c>+offset</c> after a symbol (it moves with every build), build ids, and
/// the full library path — which on Android embeds a per-install hash
/// (<c>/data/app/~~IlliRmL7ISC6U7xZvSurqg==/…</c>) and would give every device its own signature.
/// </para>
/// <para>
/// The <c>si_code</c> (<c>SEGV_MAPERR</c> vs <c>SEGV_ACCERR</c>) is deliberately kept <em>out</em> of
/// the key and reported as detail only: one bug commonly produces both depending on what garbage the
/// pointer held, so keying on it would split a single fault into two issues.
/// </para>
/// </summary>
public static partial class NativeCrashSignature
{
    /// <summary>
    /// Frames in these libraries are never chosen as the representative frame: they are where a
    /// fault *surfaces* (memcpy, the allocator, the ART runtime), not where it comes from. Keying on
    /// them would collapse unrelated bugs into one "crash in libc.so" issue.
    /// </summary>
    private static readonly string[] SystemLibraries =
    [
        "libc.so", "libc++.so", "libc++_shared.so", "libm.so", "libdl.so",
        "libart.so", "libartbase.so", "libbase.so", "libutils.so", "libcutils.so",
        "libandroid_runtime.so", "libbacktrace.so", "libunwindstack.so",
        "linker", "linker64", "libnetd_client.so",
    ];

    private static readonly Dictionary<int, string> SignalNames = new()
    {
        [4] = "SIGILL",
        [6] = "SIGABRT",
        [7] = "SIGBUS",
        [8] = "SIGFPE",
        [9] = "SIGKILL",
        [11] = "SIGSEGV",
    };

    /// <summary>
    /// Builds the title + fingerprint for a recovered crash. Never throws: this runs on the launch
    /// path of a process that has just recovered from a crash, and a parser bug must not turn a
    /// report into a second crash. Falls back to a coarse-but-valid signature whenever the trace is
    /// missing or in a shape we don't recognize.
    /// </summary>
    public static RecoveredCrashSignature Derive(ProcessExitDetails exit)
    {
        try
        {
            return DeriveCore(exit);
        }
        catch
        {
            return Fallback(exit);
        }
    }

    private static RecoveredCrashSignature DeriveCore(ProcessExitDetails exit)
    {
        var trace = exit.TraceText ?? string.Empty;

        // An ANR is not a tombstone — it is an ART thread dump with no signal and no native
        // backtrace, so running it through the native parser yields nonsense. Give it its own shape.
        if (exit.Reason.Contains("Anr", StringComparison.OrdinalIgnoreCase))
            return DeriveAnr(trace);

        var signal = ResolveSignalName(exit, trace);
        var frame = FindRepresentativeFrame(trace);
        var abortMessage = ExtractAbortMessage(trace);

        // SIGABRT is usually a deliberate abort (a runtime assertion, a mono fatal error) and its
        // abort message identifies the failure far better than whatever frame raised it.
        if (signal == "SIGABRT" && !string.IsNullOrEmpty(abortMessage))
        {
            var normalizedAbort = TelemetryMessageNormalizer.Normalize(abortMessage);
            return new RecoveredCrashSignature(
                Title: $"Native abort: {normalizedAbort}",
                Fingerprint: $"native|SIGABRT|{normalizedAbort}",
                SignalName: signal,
                Library: frame?.Library,
                Symbol: frame?.Symbol,
                FaultCode: ExtractFaultCode(trace),
                AbortMessage: abortMessage);
        }

        if (frame == null)
        {
            return new RecoveredCrashSignature(
                Title: $"Native crash {signal} (no resolvable frame)",
                Fingerprint: $"native|{signal}|unknown",
                SignalName: signal,
                Library: null,
                Symbol: null,
                FaultCode: ExtractFaultCode(trace),
                AbortMessage: abortMessage);
        }

        var title = frame.Symbol is { Length: > 0 }
            ? $"Native crash {signal} in {frame.Library}: {frame.Symbol}"
            : $"Native crash {signal} in {frame.Library}";

        return new RecoveredCrashSignature(
            Title: title,
            Fingerprint: $"native|{signal}|{frame.Library}|{frame.Symbol ?? "?"}",
            SignalName: signal,
            Library: frame.Library,
            Symbol: frame.Symbol,
            FaultCode: ExtractFaultCode(trace),
            AbortMessage: abortMessage);
    }

    private static RecoveredCrashSignature DeriveAnr(string trace)
    {
        // The main thread's topmost frame is what the app was blocked in, and it is the only part of
        // an ANR dump stable enough to key on.
        var top = ExtractMainThreadTopFrame(trace);
        return top is { Length: > 0 }
            ? new RecoveredCrashSignature(
                Title: $"ANR in {top}",
                Fingerprint: $"anr|{top}",
                SignalName: "ANR",
                Library: null, Symbol: top, FaultCode: null, AbortMessage: null)
            : new RecoveredCrashSignature(
                Title: "ANR (no main-thread frame)",
                Fingerprint: "anr|unknown",
                SignalName: "ANR",
                Library: null, Symbol: null, FaultCode: null, AbortMessage: null);
    }

    private static RecoveredCrashSignature Fallback(ProcessExitDetails exit) =>
        new(Title: $"Recovered crash ({exit.Reason})",
            Fingerprint: $"exit|{exit.Reason}",
            SignalName: SignalNames.TryGetValue(exit.Status, out var name) ? name : exit.Status.ToString(),
            Library: null, Symbol: null, FaultCode: null, AbortMessage: null);

    /// <summary>
    /// Prefers the OS-reported signal number over the trace text. <c>ApplicationExitInfo.Status</c>
    /// carries the signal for a <c>Signaled</c> exit, which is device-independent; the
    /// <c>signal 11 (SIGSEGV)</c> line is only a fallback for records that lack it.
    /// </summary>
    private static string ResolveSignalName(ProcessExitDetails exit, string trace)
    {
        if (SignalNames.TryGetValue(exit.Status, out var fromStatus))
            return fromStatus;

        var match = SignalLineRegex().Match(trace);
        if (match.Success)
        {
            var named = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(named))
                return named;
            if (int.TryParse(match.Groups[1].Value, out var number) && SignalNames.TryGetValue(number, out var mapped))
                return mapped;
        }

        return exit.Status != 0 ? $"signal {exit.Status}" : "unknown signal";
    }

    private sealed record Frame(string Library, string? Symbol);

    /// <summary>
    /// How deep to look for a frame that actually has a symbol. The faulting frame is usually inside
    /// a stripped library and resolves to nothing, while a frame or two below it sits on an exported
    /// entry point — <c>sk_surface_draw</c> rather than an anonymous address in <c>libSkiaSharp.so</c>.
    /// Bounded so a signature is never taken from unrelated code far down the stack.
    /// </summary>
    private const int SymbolSearchDepth = 8;

    /// <summary>
    /// Chooses the frame the signature is built from, in order of preference: the topmost app-owned
    /// frame that carries a symbol (most specific and still stable), then the topmost app-owned frame
    /// at all, then the topmost mapped frame — so a crash entirely inside system libraries still gets
    /// a signature instead of none.
    /// </summary>
    private static Frame? FindRepresentativeFrame(string trace)
    {
        Frame? firstMapped = null;
        Frame? firstAppOwned = null;
        var depth = 0;

        foreach (Match match in BacktraceFrameRegex().Matches(trace))
        {
            var library = ExtractLibraryName(match.Groups["map"].Value);
            if (string.IsNullOrEmpty(library))
                continue;

            var frame = new Frame(library, ExtractSymbol(match.Groups["rest"].Value));
            firstMapped ??= frame;

            if (!IsSystemLibrary(library))
            {
                firstAppOwned ??= frame;

                if (frame.Symbol is { Length: > 0 })
                    return frame;
            }

            if (++depth >= SymbolSearchDepth)
                break;
        }

        return firstAppOwned ?? firstMapped;
    }

    private static bool IsSystemLibrary(string library) =>
        SystemLibraries.Contains(library, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reduces a mapped path to the library basename. Two Android specifics matter: a library loaded
    /// straight out of a split APK appears as <c>…/split_config.arm64_v8a.apk!libSkiaSharp.so</c>
    /// (the part after <c>!</c> is the library), and the directory prefix contains a per-install
    /// hash that must never reach the signature.
    /// </summary>
    private static string ExtractLibraryName(string mapPath)
    {
        if (string.IsNullOrWhiteSpace(mapPath))
            return string.Empty;

        var name = mapPath;

        var bang = name.LastIndexOf('!');
        if (bang >= 0 && bang < name.Length - 1)
            name = name[(bang + 1)..];

        var slash = name.LastIndexOf('/');
        if (slash >= 0 && slash < name.Length - 1)
            name = name[(slash + 1)..];

        return name.Trim();
    }

    /// <summary>
    /// Picks the symbol out of a frame's trailing parenthesised groups, skipping the
    /// <c>(BuildId: …)</c> group that follows it, and drops the <c>+offset</c> suffix — the offset
    /// shifts with every build and would make the signature per-release.
    /// </summary>
    private static string? ExtractSymbol(string rest)
    {
        foreach (Match match in ParenGroupRegex().Matches(rest))
        {
            var value = match.Groups[1].Value.Trim();
            if (value.Length == 0 || value.StartsWith("BuildId", StringComparison.OrdinalIgnoreCase))
                continue;

            var plus = value.LastIndexOf('+');
            if (plus > 0)
                value = value[..plus];

            value = value.Trim();
            if (value.Length > 0)
                return value;
        }

        return null;
    }

    private static string? ExtractFaultCode(string trace)
    {
        var match = FaultCodeRegex().Match(trace);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractAbortMessage(string trace)
    {
        var match = AbortMessageRegex().Match(trace);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractMainThreadTopFrame(string trace)
    {
        var mainIndex = trace.IndexOf("\"main\"", StringComparison.Ordinal);
        if (mainIndex < 0)
            return null;

        var match = JavaFrameRegex().Match(trace, mainIndex);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// A tombstone backtrace line: <c>#00  pc 0000000000322500  /path/to.apk!lib.so (symbol+28) (BuildId: …)</c>.
    /// Frames with no mapped module (a bare <c>pc</c>, which happens when the unwinder loses the map)
    /// deliberately do not match — there is nothing stable to key on in them.
    /// </summary>
    [GeneratedRegex(@"^\s*#\d+\s+pc\s+[0-9a-fA-F]+\s+(?<map>\S+)(?<rest>[^\r\n]*)$", RegexOptions.Multiline)]
    private static partial Regex BacktraceFrameRegex();

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex ParenGroupRegex();

    [GeneratedRegex(@"signal\s+(\d+)\s*\(([A-Z]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex SignalLineRegex();

    [GeneratedRegex(@"code\s+-?\d+\s*\((SEGV_[A-Z]+|BUS_[A-Z]+|ILL_[A-Z]+|FPE_[A-Z]+|SI_[A-Z]+)\)")]
    private static partial Regex FaultCodeRegex();

    [GeneratedRegex(@"Abort message:\s*'([^']*)'")]
    private static partial Regex AbortMessageRegex();

    [GeneratedRegex(@"^\s*at\s+(\S+)", RegexOptions.Multiline)]
    private static partial Regex JavaFrameRegex();
}

/// <summary>
/// The grouping key and the human-readable parts derived from an OS exit record.
/// <see cref="Fingerprint"/> is what telemetry must group on — it is set explicitly on the outgoing
/// event rather than left to message/stack heuristics, which cannot work for an event whose "stack"
/// is a text blob from a previous process.
/// </summary>
public sealed record RecoveredCrashSignature(
    string Title,
    string Fingerprint,
    string SignalName,
    string? Library,
    string? Symbol,
    string? FaultCode,
    string? AbortMessage);
