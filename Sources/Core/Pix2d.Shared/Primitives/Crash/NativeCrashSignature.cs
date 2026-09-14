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

    /// <summary>
    /// Keys an ANR on what the <b>main thread</b> was stuck in. The dump reaching us is not always the
    /// ART thread dump this used to assume: when <c>tombstoned</c> is busy, <c>ApplicationExitInfo</c>
    /// hands back a debuggerd-style dump instead (<c>libdebuggerd_client: failed to read status
    /// response from tombstoned: Try again</c>), which has <em>no</em> <c>"main"</c> header and
    /// <em>no</em> <c>at com.foo.Bar</c> frames — the main thread is named after the truncated process
    /// name (<c>".pix2d.pix2dapp"</c>) and carries native <c>#00 pc ...</c> frames. Assuming the ART
    /// shape made every such report fall into one unactionable "no main-thread frame" bucket, which is
    /// where nearly every ANR landed in production.
    /// <para>So the main thread is located structurally (by <c>sysTid</c> == the dumped pid, else the
    /// first thread in the dump — Android always dumps main first), and the signature is taken from
    /// whichever frame shape that section actually has, falling back to the kernel wait channel for a
    /// dump that is only a "Waiting Channels" listing.</para>
    /// </summary>
    private static RecoveredCrashSignature DeriveAnr(string trace)
    {
        var main = ExtractMainThreadSection(trace);

        if (main != null)
        {
            // An ART dump: the managed/Java frame is the most specific thing available.
            var javaTop = JavaFrameRegex().Match(main);
            if (javaTop.Success && javaTop.Groups[1].Value.Length > 0)
                return Anr(javaTop.Groups[1].Value, library: null, symbol: javaTop.Groups[1].Value);

            // A debuggerd dump: same frame-picking rules as a tombstone — the topmost app-owned frame
            // that resolved to a symbol, so the allocator and the libc entry points sitting on top of a
            // blocked main thread don't become the signature.
            var frame = FindRepresentativeFrame(main);
            if (frame != null)
            {
                var where = frame.Symbol is { Length: > 0 }
                    ? $"{frame.Library}: {frame.Symbol}"
                    : frame.Library;
                return Anr(where, frame.Library, frame.Symbol);
            }
        }

        // Last resort: a dump with no backtrace at all still names the kernel wait channel the main
        // thread sat in, which at least separates a lock (futex_wait_queue) from IPC
        // (binder_thread_read) and from an idle looper (do_epoll_wait).
        var channel = ExtractMainThreadWaitChannel(trace);
        if (channel is { Length: > 0 })
            return Anr($"main thread in {channel}", library: null, symbol: channel);

        return new RecoveredCrashSignature(
            Title: "ANR (no main-thread frame)",
            Fingerprint: "anr|unknown",
            SignalName: "ANR",
            Library: null, Symbol: null, FaultCode: null, AbortMessage: null);
    }

    private static RecoveredCrashSignature Anr(string where, string? library, string? symbol) =>
        new(Title: $"ANR in {where}",
            Fingerprint: $"anr|{where}",
            SignalName: "ANR",
            Library: library, Symbol: symbol, FaultCode: null, AbortMessage: null);

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
    /// Picks the symbol out of a frame's trailing parenthesised groups, and drops the <c>+offset</c>
    /// suffix — the offset shifts with every build and would make the signature per-release.
    /// <para>Two groups around it are never the symbol. <c>(BuildId: …)</c> follows it, and a library
    /// mapped straight out of an APK — which is every native library Pix2d ships on Android — is
    /// printed with its map offset <em>before</em> it:
    /// <c>…apk!libmonosgen-2.0.so (offset 0x3198000) (mono_class_inflate_generic_method_full_checked+1208)</c>.
    /// Taking that first group put a per-process load address in the fingerprint, which splits one bug
    /// into a new issue per report — the mirror image of the collapse it is meant to prevent.</para>
    /// </summary>
    private static string? ExtractSymbol(string rest)
    {
        foreach (Match match in ParenGroupRegex().Matches(rest))
        {
            var value = match.Groups[1].Value.Trim();
            if (value.Length == 0
                || value.StartsWith("BuildId", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("offset", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// The slice of an ANR dump belonging to the main thread — from its header up to the next thread
    /// header, so a frame is never borrowed from a neighbouring thread (which would make the signature
    /// depend on whatever else happened to be running).
    /// <para>The main thread is identified, in order: the ART <c>"main"</c> header; the thread whose
    /// <c>sysTid</c> equals the dumped pid (in a debuggerd dump the main thread is named after the
    /// truncated process name, not "main"); the first thread in the dump, which is the one Android
    /// dumps first. Returns null when the trace has no thread headers at all.</para>
    /// </summary>
    private static string? ExtractMainThreadSection(string trace)
    {
        var headers = ThreadHeaderRegex().Matches(trace);
        if (headers.Count == 0)
            return null;

        var pid = ExtractDumpedPid(trace);
        var chosen = -1;

        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Groups["name"].Value == "main")
            {
                chosen = i;
                break;
            }

            if (pid == null || chosen >= 0)
                continue;

            var sysTid = SysTidRegex().Match(headers[i].Groups["rest"].Value);
            if (sysTid.Success && sysTid.Groups[1].Value == pid)
                chosen = i;
        }

        if (chosen < 0)
            chosen = 0;

        var start = headers[chosen].Index;
        var end = chosen + 1 < headers.Count ? headers[chosen + 1].Index : trace.Length;
        return trace[start..end];
    }

    private static string? ExtractDumpedPid(string trace)
    {
        var match = DumpedPidRegex().Match(trace);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The kernel wait channel of the main thread from a "Waiting Channels" listing
    /// (<c>sysTid=22998     futex_wait_queue</c>). The main thread is the one whose sysTid is the pid;
    /// with no pid to match on, the first listed — the listing is ordered the same way the dump is.
    /// </summary>
    private static string? ExtractMainThreadWaitChannel(string trace)
    {
        var pid = ExtractDumpedPid(trace);
        string? first = null;

        foreach (Match match in WaitChannelRegex().Matches(trace))
        {
            first ??= match.Groups[2].Value;
            if (pid != null && match.Groups[1].Value == pid)
                return match.Groups[2].Value;
        }

        return pid == null ? first : null;
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

    /// <summary>
    /// A thread header in either dump shape: <c>"main" prio=5 tid=1 Blocked</c> (ART) or
    /// <c>".pix2d.pix2dapp" sysTid=22998</c> (debuggerd).
    /// </summary>
    [GeneratedRegex("""^[ \t]*"(?<name>[^"\r\n]*)"(?<rest>[^\r\n]*)$""", RegexOptions.Multiline)]
    private static partial Regex ThreadHeaderRegex();

    [GeneratedRegex(@"sysTid=(\d+)")]
    private static partial Regex SysTidRegex();

    /// <summary>The pid the dump belongs to: <c>----- pid 22998 at ...</c> or <c>dumping pid: 22998</c>.</summary>
    [GeneratedRegex(@"(?:dumping\s+pid:\s*|-{2,}\s*pid\s+)(\d+)")]
    private static partial Regex DumpedPidRegex();

    [GeneratedRegex(@"^[ \t]*sysTid=(\d+)[ \t]+(\S+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex WaitChannelRegex();
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
