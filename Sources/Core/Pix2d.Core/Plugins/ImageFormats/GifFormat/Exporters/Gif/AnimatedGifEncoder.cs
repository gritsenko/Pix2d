using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Pix2d.Common.Gif;

/// <summary>
/// Animated GIF encoder that builds a single palette shared by every frame.
///
/// Pixel-art animations use a small fixed palette, so quantizing each frame
/// independently (the original NeuQuant-per-frame behaviour, which also emitted
/// a local color table per frame) made identical colors drift frame-to-frame
/// and the shades appeared to flicker. Here the palette is computed once over
/// all frames and written once as the Global Color Table:
///   * if the whole animation fits in the palette budget, colors are used
///     verbatim (no quantization at all — exact fidelity, no flicker);
///   * otherwise NeuQuant runs a single time over the combined frames so every
///     frame still maps against the same shared palette.
/// </summary>
public class AnimatedGifEncoder
{
    // A pixel is treated as transparent when its alpha is below this value.
    private const int AlphaThreshold = 128;

    protected int width; // image size
    protected int height;
    protected SKColor transparent = default(SKColor); // transparent color if given
    protected int transIndex = 0; // transparent index in color table
    protected int repeat = -1; // no repeat
    protected int delay = 0; // frame delay (hundredths)
    protected bool started = false; // ready to output frames
    protected MemoryStream ms = new();

    protected byte[] indexedPixels = []; // current frame indexed to palette
    protected int colorDepth = 8; // number of bit planes
    protected byte[] colorTab = []; // RGB palette (global color table)
    protected int palSize = 7; // color table size (bits-1) -> 256 entries
    protected int dispose = -1; // disposal code (-1 = use default)
    protected bool closeStream = false; // close stream when finished
    protected bool sizeSet = false; // if false, get size from first frame
    protected int sample = 10; // default sample interval for quantizer
    private readonly List<SKBitmap> _frames = new();

    // Shared-palette state, computed once in BuildGlobalPalette.
    private bool _hasTransparency;
    private bool _exactPalette;
    private Dictionary<int, byte>? _exactMap; // packed RGB -> palette index
    private NeuQuant? _quantizer;
    private int _firstColorSlot; // 1 when a transparent slot is reserved, else 0

    public AnimatedGifEncoder(int frameDelay, int scale)
    {
        delay = frameDelay;
        SetRepeat(0);
    }

    /// <summary>
    /// Sets the delay time between each frame in milliseconds.
    /// </summary>
    public void SetDelay(int msDelay)
    {
        delay = (int)Math.Round(msDelay / 10.0f);
    }

    /// <summary>
    /// Sets the GIF frame disposal code. Default depends on whether the
    /// animation has transparency.
    /// </summary>
    public void SetDispose(int code)
    {
        if (code >= 0)
        {
            dispose = code;
        }
    }

    /// <summary>
    /// Sets the number of times the set of GIF frames should be played.
    /// Default is 1; 0 means play indefinitely.
    /// </summary>
    public void SetRepeat(int iter)
    {
        if (iter >= 0)
        {
            repeat = iter;
        }
    }

    /// <summary>
    /// Sets a transparent color hint. Kept for API compatibility; transparency
    /// is now derived per-pixel from the frames' alpha channel.
    /// </summary>
    public void SetTransparent(SKColor c)
    {
        transparent = c;
    }

    public void AddFrame(SKBitmap im)
    {
        _frames.Add(im);
    }

    /// <summary>
    /// Flushes any pending data and writes the GIF trailer.
    /// </summary>
    public bool Finish()
    {
        if (!started) return false;
        bool ok = true;
        started = false;
        try
        {
            ms.WriteByte(0x3b); // gif trailer
            ms.Flush();
        }
        catch (IOException)
        {
            ok = false;
        }

        // reset for subsequent use
        transIndex = 0;
        indexedPixels = [];
        colorTab = [];
        closeStream = false;

        return ok;
    }

    /// <summary>
    /// Sets frame rate in frames per second.
    /// </summary>
    public void SetFrameRate(float fps)
    {
        if (fps != 0f)
        {
            delay = (int)Math.Round(100f / fps);
        }
    }

    /// <summary>
    /// Sets quality of color quantization used only when the animation exceeds
    /// the palette budget. Lower values (minimum = 1) produce better colors but
    /// are slower. 10 is the default.
    /// </summary>
    public void SetQuality(int quality)
    {
        if (quality < 1) quality = 1;
        sample = quality;
    }

    /// <summary>
    /// Sets the GIF frame size. Defaults to the size of the first frame added.
    /// </summary>
    public void SetSize(int w, int h)
    {
        width = w;
        height = h;
        if (width < 1) width = 320;
        if (height < 1) height = 240;
        sizeSet = true;
    }

    /// <summary>
    /// Initiates GIF file creation on the given stream. The stream is not
    /// closed automatically.
    /// </summary>
    public bool Start(MemoryStream os)
    {
        if (os == null) return false;
        bool ok = true;
        closeStream = false;
        ms = os;
        try
        {
            WriteString("GIF89a"); // header
        }
        catch (IOException)
        {
            ok = false;
        }
        return started = ok;
    }

    /// <summary>
    /// Initiates writing of a GIF file to a memory stream.
    /// </summary>
    public bool Start()
    {
        bool ok;
        try
        {
            ok = Start(new MemoryStream(10 * 1024));
            closeStream = true;
        }
        catch (IOException)
        {
            ok = false;
        }
        return started = ok;
    }

    public MemoryStream Output()
    {
        return ms;
    }

    public void Encode()
    {
        if (_frames.Count == 0)
        {
            Start(new MemoryStream());
            Finish();
            return;
        }

        Start(new MemoryStream());

        if (!sizeSet)
        {
            SetSize(_frames[0].Width, _frames[0].Height);
        }

        // Read every frame's pixels once; the palette is derived from the
        // actual (already-scaled) output pixels.
        var framePixels = new List<SKColor[]>(_frames.Count);
        foreach (var frame in _frames)
        {
            framePixels.Add(frame.Pixels);
        }

        BuildGlobalPalette(framePixels);

        // Header structures are written exactly once.
        WriteLSD(); // logical screen descriptor
        WritePalette(); // global color table (shared by all frames)
        if (repeat >= 0)
        {
            WriteNetscapeExt(); // loop count
        }

        foreach (var cols in framePixels)
        {
            WriteGraphicCtrlExt(); // per-frame delay / disposal
            WriteImageDesc(); // image descriptor (no local color table)
            indexedPixels = MapFrame(cols); // map against the shared palette
            WritePixels(); // encode and write pixel data
        }

        Finish();
    }

    public Stream GetResultStream()
    {
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Builds a single palette shared by every frame. When the whole animation
    /// fits the palette budget the colors are used verbatim (no quantization);
    /// otherwise a single NeuQuant pass over all frames produces a shared
    /// reduced palette. Either way there is one Global Color Table and no
    /// per-frame local tables, so colors stay consistent across the animation.
    /// </summary>
    private void BuildGlobalPalette(List<SKColor[]> framePixels)
    {
        // Scan all frames: detect transparency and collect distinct opaque colors.
        var distinct = new HashSet<int>();
        _hasTransparency = false;
        var tooManyColors = false;

        foreach (var cols in framePixels)
        {
            foreach (var c in cols)
            {
                if (c.Alpha < AlphaThreshold)
                {
                    _hasTransparency = true;
                    continue; // transparent pixels never occupy a palette slot
                }

                if (!tooManyColors)
                {
                    distinct.Add((c.Red << 16) | (c.Green << 8) | c.Blue);
                    if (distinct.Count > 256)
                    {
                        // Beyond the exact-palette budget; keep scanning only to
                        // finish transparency detection.
                        tooManyColors = true;
                    }
                }
            }
        }

        // Reserve palette index 0 for transparency only when it's actually used;
        // otherwise the full 256 slots are available for opaque colors.
        _firstColorSlot = _hasTransparency ? 1 : 0;
        transIndex = 0;
        colorDepth = 8;
        palSize = 7; // 256-entry global color table

        var maxExactColors = 256 - _firstColorSlot;

        if (!tooManyColors && distinct.Count <= maxExactColors)
        {
            // Exact path: use the animation's own colors, no quantization.
            _exactPalette = true;
            _quantizer = null;
            _exactMap = new Dictionary<int, byte>(distinct.Count);
            colorTab = new byte[3 * 256];

            var slot = _firstColorSlot;
            foreach (var key in distinct)
            {
                colorTab[slot * 3] = (byte)((key >> 16) & 0xff);
                colorTab[slot * 3 + 1] = (byte)((key >> 8) & 0xff);
                colorTab[slot * 3 + 2] = (byte)(key & 0xff);
                _exactMap[key] = (byte)slot;
                slot++;
            }
        }
        else
        {
            // Quantized fallback: one NeuQuant pass over all frames combined so
            // every frame maps against the same reduced palette.
            _exactPalette = false;
            _exactMap = null;

            long totalPixels = 0;
            foreach (var cols in framePixels)
            {
                totalPixels += cols.Length;
            }

            var rgb = new byte[totalPixels * 3];
            var p = 0;
            foreach (var cols in framePixels)
            {
                foreach (var c in cols)
                {
                    rgb[p++] = c.Red;
                    rgb[p++] = c.Green;
                    rgb[p++] = c.Blue;
                }
            }

            _quantizer = new NeuQuant(rgb, rgb.Length, sample);
            // Process(offset) shifts the reduced colors so the reserved
            // transparent slot (when present) stays at index 0.
            colorTab = _quantizer.Process(_firstColorSlot);
        }
    }

    /// <summary>
    /// Maps one frame's pixels to indices in the shared global palette.
    /// </summary>
    private byte[] MapFrame(SKColor[] cols)
    {
        var idx = new byte[cols.Length];
        for (var i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (_hasTransparency && c.Alpha < AlphaThreshold)
            {
                idx[i] = 0; // transparent index
                continue;
            }

            if (_exactPalette)
            {
                idx[i] = _exactMap![(c.Red << 16) | (c.Green << 8) | c.Blue];
            }
            else
            {
                idx[i] = (byte)(_quantizer!.Map(c.Red, c.Green, c.Blue) + _firstColorSlot);
            }
        }
        return idx;
    }

    /// <summary>
    /// Writes Graphic Control Extension for the current frame.
    /// </summary>
    protected void WriteGraphicCtrlExt()
    {
        ms.WriteByte(0x21); // extension introducer
        ms.WriteByte(0xf9); // GCE label
        ms.WriteByte(4); // data block size

        int transp, disp;
        if (_hasTransparency)
        {
            transp = 1;
            disp = 2; // restore to background so transparency shows through
        }
        else
        {
            transp = 0;
            disp = 1; // leave frame in place (full-frame opaque renders)
        }

        if (dispose >= 0)
        {
            disp = dispose & 7; // user override
        }
        disp <<= 2;

        // packed fields: 1:3 reserved | 4:6 disposal | 7 user input | 8 transparency
        var byt = (byte)(disp | transp);
        ms.WriteByte(byt);

        WriteShort(delay); // delay x 1/100 sec
        ms.WriteByte((byte)transIndex); // transparent color index
        ms.WriteByte(0); // block terminator
    }

    /// <summary>
    /// Writes Image Descriptor. All frames reference the global color table, so
    /// no local color table is ever emitted.
    /// </summary>
    protected void WriteImageDesc()
    {
        ms.WriteByte(0x2c); // image separator
        WriteShort(0); // image position x,y = 0,0
        WriteShort(0);
        WriteShort(width); // image size
        WriteShort(height);
        ms.WriteByte(0); // no local color table -> use the global color table
    }

    /// <summary>
    /// Writes Logical Screen Descriptor.
    /// </summary>
    protected void WriteLSD()
    {
        // logical screen size
        WriteShort(width);
        WriteShort(height);
        // packed fields
        ms.WriteByte(Convert.ToByte(0x80 | // 1   : global color table flag = 1 (gct used)
                                    0x70 | // 2-4 : color resolution = 7
                                    0x00 | // 5   : gct sort flag = 0
                                    palSize)); // 6-8 : gct size

        ms.WriteByte(0); // background color index
        ms.WriteByte(0); // pixel aspect ratio - assume 1:1
    }

    /// <summary>
    /// Writes Netscape application extension to define repeat count.
    /// </summary>
    protected void WriteNetscapeExt()
    {
        ms.WriteByte(0x21); // extension introducer
        ms.WriteByte(0xff); // app extension label
        ms.WriteByte(11); // block size
        WriteString("NETSCAPE" + "2.0"); // app id + auth code
        ms.WriteByte(3); // sub-block size
        ms.WriteByte(1); // loop sub-block id
        WriteShort(repeat); // loop count (extra iterations, 0=repeat forever)
        ms.WriteByte(0); // block terminator
    }

    /// <summary>
    /// Writes the color table, padded to 256 entries.
    /// </summary>
    protected void WritePalette()
    {
        ms.Write(colorTab, 0, colorTab.Length);
        int n = (3 * 256) - colorTab.Length;
        for (int i = 0; i < n; i++)
        {
            ms.WriteByte(0);
        }
    }

    /// <summary>
    /// Encodes and writes pixel data for the current frame.
    /// </summary>
    protected void WritePixels()
    {
        LZWEncoder encoder =
            new LZWEncoder(width, height, indexedPixels, colorDepth);
        encoder.Encode(ms);
    }

    /// <summary>
    /// Write 16-bit value to output stream, LSB first.
    /// </summary>
    protected void WriteShort(int value)
    {
        ms.WriteByte(Convert.ToByte(value & 0xff));
        ms.WriteByte(Convert.ToByte((value >> 8) & 0xff));
    }

    /// <summary>
    /// Writes string to output stream.
    /// </summary>
    protected void WriteString(String s)
    {
        char[] chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            ms.WriteByte((byte)chars[i]);
        }
    }
}
