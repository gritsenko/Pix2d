using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Pix2d.Common.Gif;

public class AnimatedGifEncoder
{
    protected int width; // image size
    protected int height;
    protected SKColor transparent = default(SKColor); // transparent color if given
    protected int transIndex = 0; // transparent index in color table
    protected int repeat = -1; // no repeat
    protected int delay = 0; // frame delay (hundredths)
    protected bool started = false; // ready to output frames
    //	protected BinaryWriter bw;
    protected MemoryStream  ms;
//		protected FileStream fs;

    protected SKBitmap image; // current frame
    protected byte[] pixels; // BGR byte array from frame
    protected byte[] indexedPixels; // converted frame indexed to palette
    protected int colorDepth; // number of bit planes
    protected byte[] colorTab; // RGB palette
    protected bool[] usedEntry = new bool[256]; // active palette entries
    protected int palSize = 7; // color table size (bits-1)
    protected int dispose = -1; // disposal code (-1 = use default)
    protected bool closeStream = false; // close stream when finished
    protected bool firstFrame = true;
    protected bool sizeSet = false; // if false, get size from first frame
    protected int sample = 10; // default sample interval for quantizer
    private List<SKBitmap> _frames = new();

    public AnimatedGifEncoder(int frameDelay, int scale)
    {
        delay = frameDelay;
        SetRepeat(0);
    }

    /**
     * Sets the delay time between each frame, or changes it
     * for subsequent frames (applies to last frame added).
     *
     * @param ms int delay time in milliseconds
     */
    public void SetDelay(int ms) 
    {
        delay = ( int ) Math.Round(ms / 10.0f);
    }
	
    /**
     * Sets the GIF frame disposal code for the last added frame
     * and any subsequent frames.  Default is 0 if no transparent
     * color has been set, otherwise 2.
     * @param code int disposal code.
     */
    public void SetDispose(int code) 
    {
        if (code >= 0) 
        {
            dispose = code;
        }
    }
	
    /**
     * Sets the number of times the set of GIF frames
     * should be played.  Default is 1; 0 means play
     * indefinitely.  Must be invoked before the first
     * image is added.
     *
     * @param iter int number of iterations.
     * @return
     */
    public void SetRepeat(int iter) 
    {
        if (iter >= 0) 
        {
            repeat = iter;
        }
    }
	
    /**
     * Sets the transparent color for the last added frame
     * and any subsequent frames.
     * Since all colors are subject to modification
     * in the quantization process, the color in the final
     * palette for each frame closest to the given color
     * becomes the transparent color for that frame.
     * May be set to null to indicate no transparent color.
     *
     * @param c Color to be treated as transparent on display.
     */
    public void SetTransparent(SKColor c) 
    {
        transparent = c;
    }

    public void AddFrame(SKBitmap im)
    {
        _frames.Add(im);
    }

    /**
     * Adds next GIF frame.  The frame is not written immediately, but is
     * actually deferred until the next frame is received so that timing
     * data can be inserted.  Invoking <code>finish()</code> flushes all
     * frames.  If <code>setSize</code> was not invoked, the size of the
     * first image is used for all subsequent frames.
     *
     * @param im BufferedImage containing frame to write.
     * @return true if successful.
     */
    private bool AddFrameCore(SKBitmap im) 
    {
        if ((im == null) || !started) 
        {
            return false;
        }
        bool ok = true;
        try 
        {
            if (!sizeSet) 
            {
                // use first frame's size
                SetSize((int) im.Width, (int) im.Height);
            }
            image = im;
            AnalyzePixels(); // build color table & map pixels
            if (firstFrame) 
            {
                WriteLSD(); // logical screen descriptior
                WritePalette(); // global color table
                if (repeat >= 0) 
                {
                    // use NS app extension to indicate reps
                    WriteNetscapeExt();
                }
            }
            WriteGraphicCtrlExt(); // write graphic control extension
            WriteImageDesc(); // image descriptor
            if (!firstFrame) 
            {
                WritePalette(); // local color table
            }
            WritePixels(); // encode and write pixel data
            firstFrame = false;
        } 
        catch (IOException e) 
        {
            ok = false;
        }

        return ok;
    }
	
    /**
     * Flushes any pending data and closes output file.
     * If writing to an OutputStream, the stream is not
     * closed.
     */
    public bool Finish() 
    {
        if (!started) return false;
        bool ok = true;
        started = false;
        try 
        {
            ms.WriteByte( 0x3b ); // gif trailer
            ms.Flush();
            if (closeStream) 
            {
//					ms.Close();
            }
        } 
        catch (IOException e) 
        {
            ok = false;
        }

        // reset for subsequent use
        transIndex = 0;
//			fs = null;
        image = null;
        pixels = null;
        indexedPixels = null;
        colorTab = null;
        closeStream = false;
        firstFrame = true;

        return ok;
    }
	
    /**
     * Sets frame rate in frames per second.  Equivalent to
     * <code>setDelay(1000/fps)</code>.
     *
     * @param fps float frame rate (frames per second)
     */
    public void SetFrameRate(float fps) 
    {
        if (fps != 0f) 
        {
            delay = ( int ) Math.Round(100f / fps);
        }
    }
	
    /**
     * Sets quality of color quantization (conversion of images
     * to the maximum 256 colors allowed by the GIF specification).
     * Lower values (minimum = 1) produce better colors, but slow
     * processing significantly.  10 is the default, and produces
     * good color mapping at reasonable speeds.  Values greater
     * than 20 do not yield significant improvements in speed.
     *
     * @param quality int greater than 0.
     * @return
     */
    public void SetQuality(int quality) 
    {
        if (quality < 1) quality = 1;
        sample = quality;
    }
	
    /**
     * Sets the GIF frame size.  The default size is the
     * size of the first frame added if this method is
     * not invoked.
     *
     * @param w int frame width.
     * @param h int frame width.
     */
    public void SetSize(int w, int h) 
    {
        if (started && !firstFrame) return;
        width = w;
        height = h;
        if (width < 1) width = 320;
        if (height < 1) height = 240;
        sizeSet = true;
    }
	
    /**
     * Initiates GIF file creation on the given stream.  The stream
     * is not closed automatically.
     *
     * @param os OutputStream on which GIF images are written.
     * @return false if initial write failed.
     */

    public bool Start( MemoryStream os) 
    {
        if (os == null) return false;
        bool ok = true;
        closeStream = false;
        ms = os;
        try 
        {
            WriteString("GIF89a"); // header
        } 
        catch (IOException e) 
        {
            ok = false;
        }
        return started = ok;
    }

    /**
     * Initiates writing of a GIF file to a memory stream.
     *
     * @return false if open or initial write failed.
     */
    public bool Start() 
    {
        bool ok = true;
        try 
        {
            ok = Start(new MemoryStream(10*1024));
            closeStream = true;
        } 
        catch (IOException e) 
        {
            ok = false;
        }
        return started = ok;
    }

    /**
     * Initiates writing of a GIF file with the specified name.
     *
     * @return false if open or initial write failed.
     */
    //public bool Output(string file) 
    //{
    //	try 
    //	{
    //		FileStream fs = new FileStream( file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None );
    //		fs.Write(ms.ToArray(),0,(int) ms.Length);
    //		fs.Close();
    //	}
    //	catch (IOException e) 
    //	{
    //		return false;
    //	}
    //	return true;
    //}
				
    public MemoryStream Output()
    {
        return ms;
    }

    /**
     * Analyzes image colors and creates color map.
     */
    protected void AnalyzePixels() 
    {
        var cols = GetImagePixels(); // convert to correct format if necessary

        int len = pixels.Length;
        int nPix = len / 3;
        indexedPixels = new byte[nPix];
        NeuQuant nq = new NeuQuant(pixels, len, sample);
        // initialize quantizer
        colorTab = nq.Process(1); // create reduced palette
        // map image pixels to new palette
        int k = 0;
        for (int i = 0; i < nPix; i++) 
        {
            int index =
                nq.Map(pixels[k++] & 0xff,
                    pixels[k++] & 0xff,
                    pixels[k++] & 0xff);
            usedEntry[index] = true;
            indexedPixels[i] = (byte) (index + 1);

            if (cols[i].Alpha < 127)
            {
                indexedPixels[i] = 0;
            }
        }
        pixels = null;
        colorDepth = 8;
        palSize = 7;
        // get closest match to transparent color if specified
        //if (transparent != Color.Empty ) 
        {
            //transIndex = FindClosest(transparent);
            transIndex = 0;//nq.Map(transparent.B, transparent.G, transparent.R);
        }


    }
	
    /**
     * Returns index of palette color closest to c
     *
     */
    protected int FindClosest(SKColor c) 
    {
        if (colorTab == null) return -1;
        int r = c.Red;
        int g = c.Green;
        int b = c.Blue;
        int minpos = 0;
        int dmin = 256 * 256 * 256;
        int len = colorTab.Length;
        for (int i = 0; i < len;) 
        {
            int dr = r - (colorTab[i++] & 0xff);
            int dg = g - (colorTab[i++] & 0xff);
            int db = b - (colorTab[i] & 0xff);
            int d = dr * dr + dg * dg + db * db;
            int index = i / 3;
            if (usedEntry[index] && (d < dmin)) 
            {
                dmin = d;
                minpos = index;
            }
            i++;
        }
        return minpos;
    }
	
    /**
     * Extracts image pixels into byte array "pixels"
     */
    protected SKColor[] GetImagePixels() 
    {
        int w = (int) image.Width;
        int h = (int) image.Height;
        //		int type = image.GetType().;
        //if ((w != width)
        //	|| (h != height)
        //	) 
        //{
        //	// create new image with right size/format
        //	Image temp =
        //		new Bitmap(width, height );
        //	Graphics g = Graphics.FromImage( temp );
        //	g.DrawImage(image, 0, 0);
        //	image = temp;
        //	g.Dispose();
        //}
        /*
            ToDo:
            improve performance: use unsafe code
        */
        pixels = new Byte [ 3 * w * h ];
        int count = 0;
        //Bitmap tempBitmap = new Bitmap( image );
        var cols = image.Pixels;
        for (int th = 0; th < h; th++)
        {
            for (int tw = 0; tw < w; tw++)
            {
                var color = cols[w*th + tw];
                //Color color = tempBitmap.PickColorByPoint(tw, th);
                pixels[count] = color.Red;
                count++;
                pixels[count] = color.Green;
                count++;
                pixels[count] = color.Blue;
                count++;
            }
        }
        return cols;
        //		pixels = ((DataBufferByte) image.getRaster().getDataBuffer()).getData();
    }
	
    /**
     * Writes Graphic Control Extension
     */
    protected void WriteGraphicCtrlExt() 
    {
        ms.WriteByte(0x21); // extension introducer
        ms.WriteByte(0xf9); // GCE label
        ms.WriteByte(4); // data block size
        int transp, disp;
        //if (transparent == Color.Empty ) 
        //{
        //	transp = 0;
        //	disp = 0; // dispose = no action
        //} 
        //else 
        //{
        //	transp = 1;
        //	disp = 2; // force clear if using transparent color
        //}

        transp = 1;
        disp = 2; // force clear if using transparent color

        if (dispose >= 0) 
        {
            disp = dispose & 7; // user override
        }
        disp <<= 2;

        // packed fields
        var byt = Convert.ToByte(0 | // 1:3 reserved
                                 disp | // 4:6 disposal
                                 0 | // 7   user input - 0 = none
                                 transp);

        ms.WriteByte( byt); // 8   transparency flag

        WriteShort(delay); // delay x 1/100 sec
        ms.WriteByte( Convert.ToByte( transIndex)); // transparent color index
        ms.WriteByte(0); // block terminator
    }
	
    /**
     * Writes Image Descriptor
     */
    protected void WriteImageDesc()
    {
        ms.WriteByte(0x2c); // image separator
        WriteShort(0); // image position x,y = 0,0
        WriteShort(0);
        WriteShort(width); // image size
        WriteShort(height);
        // packed fields
        if (firstFrame) 
        {
            // no LCT  - GCT is used for first (or only) frame
            ms.WriteByte(0);
        } 
        else 
        {
            // specify normal LCT
            ms.WriteByte( Convert.ToByte( 0x80 | // 1 local color table  1=yes
                                          0 | // 2 interlace - 0=no
                                          0 | // 3 sorted - 0=no
                                          0 | // 4-5 reserved
                                          palSize ) ); // 6-8 size of color table
        }
    }
	
    /**
     * Writes Logical Screen Descriptor
     */
    protected void WriteLSD()  
    {
        // logical screen size
        WriteShort(width);
        WriteShort(height);
        // packed fields
        ms.WriteByte( Convert.ToByte (0x80 | // 1   : global color table flag = 1 (gct used)
                                      0x70 | // 2-4 : color resolution = 7
                                      0x00 | // 5   : gct sort flag = 0
                                      palSize) ); // 6-8 : gct size

        ms.WriteByte(0); // background color index
        ms.WriteByte(0); // pixel aspect ratio - assume 1:1
    }
	
    /**
     * Writes Netscape application extension to define
     * repeat count.
     */
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
	
    /**
     * Writes color table
     */
    protected void WritePalette()
    {
        ms.Write(colorTab, 0, colorTab.Length);
        int n = (3 * 256) - colorTab.Length;
        for (int i = 0; i < n; i++) 
        {
            ms.WriteByte(0);
        }
    }
	
    /**
     * Encodes and writes pixel data
     */
    protected void WritePixels()
    {
        LZWEncoder encoder =
            new LZWEncoder(width, height, indexedPixels, colorDepth);
        encoder.Encode( ms );
    }
	
    /**
     *    Write 16-bit value to output stream, LSB first
     */
    protected void WriteShort(int value)
    {
        ms.WriteByte( Convert.ToByte( value & 0xff));
        ms.WriteByte( Convert.ToByte( (value >> 8) & 0xff ));
    }
	
    /**
     * Writes string to output stream
     */
    protected void WriteString(String s)
    {
        char[] chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++) 
        {
            ms.WriteByte((byte) chars[i]);
        }
    }

    public void Encode()
    {
        Start(new MemoryStream());
            
        foreach (var frame in _frames)
        {
            AddFrameCore(frame);
        }

        Finish();
    }

    public Stream GetResultStream()
    {
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}