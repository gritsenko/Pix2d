#nullable enable
using System.Text;

namespace Pix2d.Primitives.Crash;

/// <summary>
/// Turns Android's <b>binary</b> tombstone into the text shape <see cref="NativeCrashSignature"/>
/// already understands.
///
/// <para><c>ApplicationExitInfo.getTraceInputStream()</c> does not always hand back the text tombstone.
/// On recent Android it is frequently the protobuf-encoded <c>Tombstone</c> message instead, and the
/// reader on the head used to pull that through a <see cref="StreamReader"/> — i.e. decode arbitrary
/// bytes as UTF-8. Every invalid sequence became U+FFFD, so the payload was destroyed before any parser
/// saw it, the frame regexes matched nothing, and 40 crashes over 20 users collapsed into
/// <c>Native crash SIGSEGV (no resolvable frame)</c>: the second-largest signature in appstat, with no
/// frame to act on.</para>
///
/// <para>Rather than teach the signature code a second input format, the parts that matter are decoded
/// here and <em>re-rendered as a text tombstone</em>. Everything <see cref="NativeCrashSignature"/>
/// already does — preferring an app-owned library, skipping the allocator and the ART runtime, stripping
/// the per-install path hash and the symbol <c>+offset</c> — then applies unchanged, and a protobuf
/// report groups identically to the same fault reported as text.</para>
///
/// <para><b>Fail-safe by construction.</b> Any payload that is not a well-formed tombstone — including a
/// text one, which must keep taking the existing path — returns null, and the caller falls back to
/// exactly today's behavior. A wrong guess costs nothing beyond what is already lost.</para>
/// </summary>
public static class TombstoneProtoReader
{
    // Field numbers read off a real production payload (the strings adjacent to each tag identify them
    // unambiguously), and matching AOSP's tombstone.proto.
    private const int TombstoneSignalInfo = 10;   // Signal
    private const int TombstoneThreads = 17;      // map<int32, Thread>

    private const int SignalName = 2;             // string, e.g. "SIGSEGV"
    private const int SignalCode = 3;             // int32
    private const int SignalCodeName = 4;         // string, e.g. "SEGV_MAPERR"

    private const int MapEntryValue = 2;          // Thread

    private const int ThreadName = 2;             // string
    private const int ThreadBacktrace = 4;        // repeated BacktraceFrame

    private const int FrameFunctionName = 4;      // string
    private const int FrameFileName = 6;          // string, the mapped library
    private const int FrameBuildId = 8;           // string

    private const int WireVarint = 0;
    private const int WireFixed64 = 1;
    private const int WireLengthDelimited = 2;
    private const int WireFixed32 = 5;

    /// <summary>
    /// Renders <paramref name="payload"/> as a text tombstone, or null when it is not a protobuf
    /// tombstone (a text tombstone included) or carries no usable backtrace.
    /// </summary>
    public static string? TryRenderAsText(byte[] payload)
    {
        try
        {
            return Render(payload);
        }
        catch
        {
            // This runs while recovering from a crash. A malformed payload must never produce a second one.
            return null;
        }
    }

    private static string? Render(byte[] payload)
    {
        if (payload.Length == 0 || LooksLikeText(payload))
            return null;

        string? signalName = null, codeName = null;
        var code = 0;
        var threads = new List<List<Frame>>();

        var reader = new Reader(payload, 0, payload.Length);
        while (reader.TryReadTag(out var field, out var wire))
        {
            switch (field)
            {
                case TombstoneSignalInfo when wire == WireLengthDelimited:
                    ReadSignal(reader.ReadMessage(), ref signalName, ref code, ref codeName);
                    break;

                case TombstoneThreads when wire == WireLengthDelimited:
                    var thread = ReadThread(reader.ReadMessage());
                    if (thread.Count > 0)
                        threads.Add(thread);
                    break;

                default:
                    reader.SkipField(wire);
                    break;
            }
        }

        var frames = SelectThread(threads);
        if (frames == null)
            return null;

        // '\n', never Environment.NewLine: this text exists only to be fed straight back into
        // NativeCrashSignature, a real tombstone is LF-terminated, and the line-anchored frame regex
        // silently matches nothing when a stray CR sits before the newline.
        var text = new StringBuilder();

        // Only the names are emitted: the signal number comes from ApplicationExitInfo.Status, which is
        // the value NativeCrashSignature prefers anyway, and the code number is never read.
        if (signalName != null)
            text.Append("signal 0 (").Append(signalName).Append(')');
        else
            text.Append("signal 0 (UNKNOWN)");

        if (codeName != null)
            text.Append(", code ").Append(code).Append(" (").Append(codeName).Append(')');

        text.Append('\n');
        text.Append("backtrace:").Append('\n');

        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];

            // The exact line shape BacktraceFrameRegex expects: "#00  pc <hex>  <map> (sym+0) (BuildId: …)".
            // The pc is rendered as zero on purpose — it is per-process and excluded from every signature,
            // so carrying the real one would only risk it leaking into a title.
            text.Append("  #").Append(i.ToString("D2")).Append("  pc 0000000000000000  ").Append(frame.FileName);

            if (!string.IsNullOrEmpty(frame.FunctionName))
                text.Append(" (").Append(frame.FunctionName).Append(')');

            if (!string.IsNullOrEmpty(frame.BuildId))
                text.Append(" (BuildId: ").Append(frame.BuildId).Append(')');

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Which thread the signature is taken from. The faulting thread is <em>not</em> reliably the first
    /// one in the map, so the choice is the first thread carrying a frame in a library that is ours —
    /// the same preference <see cref="NativeCrashSignature.FindRepresentativeFrame"/> applies within a
    /// thread, and deterministic for a given payload, which is what keeps grouping stable. A dump with
    /// nothing app-owned anywhere falls back to the first thread that has any frames at all.
    /// </summary>
    private static List<Frame>? SelectThread(List<List<Frame>> threads)
    {
        foreach (var thread in threads)
            if (thread.Any(f => NativeCrashSignature.IsAppOwnedLibraryPath(f.FileName)))
                return thread;

        return threads.FirstOrDefault();
    }

    private static void ReadSignal(Reader reader, ref string? name, ref int code, ref string? codeName)
    {
        while (reader.TryReadTag(out var field, out var wire))
        {
            switch (field)
            {
                case SignalName when wire == WireLengthDelimited:
                    name = reader.ReadString();
                    break;
                case SignalCode when wire == WireVarint:
                    code = (int)reader.ReadVarint();
                    break;
                case SignalCodeName when wire == WireLengthDelimited:
                    codeName = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wire);
                    break;
            }
        }
    }

    /// <summary>
    /// A <c>map&lt;int32, Thread&gt;</c> entry: the key is field 1 and the <c>Thread</c> itself field 2.
    /// </summary>
    private static List<Frame> ReadThread(Reader entry)
    {
        var frames = new List<Frame>();

        while (entry.TryReadTag(out var field, out var wire))
        {
            if (field != MapEntryValue || wire != WireLengthDelimited)
            {
                entry.SkipField(wire);
                continue;
            }

            var thread = entry.ReadMessage();
            while (thread.TryReadTag(out var threadField, out var threadWire))
            {
                switch (threadField)
                {
                    case ThreadBacktrace when threadWire == WireLengthDelimited:
                        var frame = ReadFrame(thread.ReadMessage());
                        if (!string.IsNullOrEmpty(frame.FileName))
                            frames.Add(frame);
                        break;
                    case ThreadName when threadWire == WireLengthDelimited:
                        thread.ReadString();
                        break;
                    default:
                        thread.SkipField(threadWire);
                        break;
                }
            }
        }

        return frames;
    }

    private static Frame ReadFrame(Reader reader)
    {
        string? file = null, function = null, buildId = null;

        while (reader.TryReadTag(out var field, out var wire))
        {
            switch (field)
            {
                case FrameFunctionName when wire == WireLengthDelimited:
                    function = reader.ReadString();
                    break;
                case FrameFileName when wire == WireLengthDelimited:
                    file = reader.ReadString();
                    break;
                case FrameBuildId when wire == WireLengthDelimited:
                    buildId = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wire);
                    break;
            }
        }

        return new Frame(file ?? string.Empty, function, buildId);
    }

    /// <summary>
    /// A text tombstone must keep taking the existing path. Protobuf payloads start with a tag byte and
    /// are full of control bytes; a tombstone and an ANR dump are printable text from the first line.
    /// </summary>
    private static bool LooksLikeText(byte[] payload)
    {
        var sampled = Math.Min(payload.Length, 512);
        for (var i = 0; i < sampled; i++)
        {
            var b = payload[i];
            if (b is 0x09 or 0x0A or 0x0D || b >= 0x20)
                continue;

            return false;
        }

        return true;
    }

    private readonly record struct Frame(string FileName, string? FunctionName, string? BuildId);

    /// <summary>
    /// A minimal protobuf wire-format cursor: enough to walk the fields above and step over everything
    /// else by wire type, which is what makes the decoder tolerant of schema drift between OS versions.
    /// </summary>
    private sealed class Reader(byte[] buffer, int start, int end)
    {
        private int _position = start;

        public bool TryReadTag(out int field, out int wire)
        {
            field = 0;
            wire = 0;

            if (_position >= end)
                return false;

            var tag = ReadVarint();
            field = (int)(tag >> 3);
            wire = (int)(tag & 7);
            return field > 0;
        }

        public ulong ReadVarint()
        {
            ulong value = 0;
            var shift = 0;

            while (_position < end)
            {
                var b = buffer[_position++];
                value |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    return value;

                shift += 7;
                if (shift > 63)
                    throw new InvalidDataException("Malformed varint.");
            }

            throw new InvalidDataException("Truncated varint.");
        }

        public Reader ReadMessage()
        {
            var length = ReadLength();
            var message = new Reader(buffer, _position, _position + length);
            _position += length;
            return message;
        }

        public string ReadString()
        {
            var length = ReadLength();
            var value = Encoding.UTF8.GetString(buffer, _position, length);
            _position += length;
            return value;
        }

        public void SkipField(int wire)
        {
            switch (wire)
            {
                case WireVarint:
                    ReadVarint();
                    break;
                case WireFixed64:
                    Advance(8);
                    break;
                case WireLengthDelimited:
                    Advance(ReadLength());
                    break;
                case WireFixed32:
                    Advance(4);
                    break;
                default:
                    // An unknown wire type means the cursor is no longer aligned to a field boundary;
                    // there is nothing safe to skip, so abandon the payload rather than read garbage.
                    throw new InvalidDataException($"Unsupported wire type {wire}.");
            }
        }

        private int ReadLength()
        {
            var length = (long)ReadVarint();
            if (length < 0 || _position + length > end)
                throw new InvalidDataException("Length-delimited field runs past its parent.");

            return (int)length;
        }

        private void Advance(int count)
        {
            if (_position + count > end)
                throw new InvalidDataException("Field runs past its parent.");

            _position += count;
        }
    }
}
