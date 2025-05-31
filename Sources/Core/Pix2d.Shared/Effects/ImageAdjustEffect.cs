using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;
public sealed class ImageAdjustEffect : ISKNodeEffect
{
    private SKColorFilter _colorFilter;

    public string Name => "Image Adjust";
    public EffectType EffectType { get; } = EffectType.OverlayEffect;

    public float Hue { get; set; }

    public float Lightness { get; set; }

    public float Saturation { get; set; }

    public void Render(RenderContext rc, SKSurface source)
    {
        if(_colorFilter == null)
            Invalidate();

        using var paint = new SKPaint();
        paint.ColorFilter = _colorFilter;
        rc.Canvas.DrawSurface(source, 0, 0, paint);
    }

    public void Invalidate()
    {
        var colorMatrix = new ColorMatrix();

        ApplyHue(colorMatrix, Hue);
        ApplyBrightness(colorMatrix, Lightness);
        ApplySaturation(colorMatrix, Saturation);

        _colorFilter = SKColorFilter.CreateColorMatrix(colorMatrix.GetArray());
    }

    private void ApplyHue(ColorMatrix matrix, float value)
    {
        value = Math.Clamp(value, -180f, 180f) / 180f * (float)Math.PI;
        if (value == 0)
            return;

        var cosVal = (float)Math.Cos(value);
        var sinVal = (float)Math.Sin(value);

        const float lumR = 0.213f;
        const float lumG = 0.715f;
        const float lumB = 0.072f;

        var mat = new[]
        {
                lumR + cosVal * (1 - lumR) + sinVal * (-lumR), lumG + cosVal * (-lumG) + sinVal * (-lumG), lumB + cosVal * (-lumB) + sinVal * (1 - lumB), 0, 0,
                lumR + cosVal * (-lumR) + sinVal * (0.143f), lumG + cosVal * (1 - lumG) + sinVal * (0.140f), lumB + cosVal * (-lumB) + sinVal * (-0.283f), 0, 0,
                lumR + cosVal * (-lumR) + sinVal * (-(1 - lumR)), lumG + cosVal * (-lumG) + sinVal * (lumG), lumB + cosVal * (1 - lumB) + sinVal * (lumB), 0, 0,
                0f, 0f, 0f, 1f, 0f,
                0f, 0f, 0f, 0f, 1f
            };

        matrix.PostConcat(new ColorMatrix(mat));
    }

    private void ApplyBrightness(ColorMatrix matrix, float value)
    {
        value = Math.Clamp(value, -100f, 100f) / 100f;

        if (value == 0)
            return;

        var mat = new[]
        {
                1,0,0,0,value,
                0,1,0,0,value,
                0,0,1,0,value,
                0,0,0,1,0,
                0,0,0,0,1
            };

        matrix.PostConcat(new ColorMatrix(mat));
    }

    private void ApplySaturation(ColorMatrix matrix, float value)
    {
        value = Math.Clamp(value, -100f, 100f);
        if (value == 0)
            return;

        var x = 1 + ((value > 0) ? 3 * value / 100 : value / 100);

        const float lumR = 0.3086f;
        const float lumG = 0.6094f;
        const float lumB = 0.0820f;

        var mat = new[]
        {
                lumR*(1-x)+x,lumG*(1-x),lumB*(1-x),0,0,
                lumR*(1-x),lumG*(1-x)+x,lumB*(1-x),0,0,
                lumR*(1-x),lumG*(1-x),lumB*(1-x)+x,0,0,
                0,0,0,1,0,
                0,0,0,0,1
            };

        matrix.PostConcat(new ColorMatrix(mat));
    }
}

public class ColorMatrix
{
    private readonly float[] _array = new float[20];

    /// <summary>
    /// Create a new ColorMatrix initialized to identity (as if reset() had
    /// been called).
    /// </summary>
    public ColorMatrix()
    {
        Reset();
    }

    /// <summary>
    /// Create a new ColorMatrix initialized with the specified array of values.
    /// </summary>
    public ColorMatrix(float[] src)
    {
        Array.Copy(src, 0, _array, 0, 20);
    }

    /// <summary>
    /// Create a new ColorMatrix initialized with the specified ColorMatrix.
    /// </summary>
    public ColorMatrix(ColorMatrix src)
    {
        Array.Copy(src._array, 0, _array, 0, 20);
    }

    /// <summary>
    /// Return the array of floats representing this ColorMatrix.
    /// </summary>
    public float[] GetArray() { return _array; }

    /// <summary>
    /// Set this ColorMatrix to identity:
    /// [ 1 0 0 0 0   - red vector
    ///   0 1 0 0 0   - green vector
    ///   0 0 1 0 0   - blue vector
    ///   0 0 0 1 0 ] - alpha vector
    /// </summary>
    public void Reset()
    {
        var a = _array;

        for (var i = 19; i > 0; --i)
        {
            a[i] = 0;
        }
        a[0] = a[6] = a[12] = a[18] = 1;
    }

    /// <summary>
    /// Assign the src ColorMatrix into this matrix, copying all of its values.
    /// </summary>
    public void Set(ColorMatrix src)
    {
        Array.Copy(src._array, 0, _array, 0, 20);
    }

    /// <summary>
    /// Assign the array of floats into this matrix, copying all of its values.
    /// </summary>
    public void Set(float[] src)
    {
        Array.Copy(src, 0, _array, 0, 20);
    }

    /// <summary>
    /// Set this ColorMatrix to scale by the specified values.
    /// </summary>
    public void SetScale(float rScale, float gScale, float bScale,
        float aScale)
    {
        var a = _array;
        for (var i = 19; i > 0; --i)
        {
            a[i] = 0;
        }
        a[0] = rScale;
        a[6] = gScale;
        a[12] = bScale;
        a[18] = aScale;
    }

    /// <summary>
    /// Set the rotation on a color axis by the specified values.
    /// axis=0 correspond to a rotation around the RED color
    /// axis=1 correspond to a rotation around the GREEN color
    /// axis=2 correspond to a rotation around the BLUE color
    /// </summary>
    public void SetRotate(int axis, float degrees)
    {
        Reset();
        var radians = degrees * Math.PI / 180f;
        var cosine = (float)Math.Cos(radians);
        var sine = (float)Math.Sin(radians);
        switch (axis)
        {
            // Rotation around the red color
            case 0:
                _array[6] = _array[12] = cosine;
                _array[7] = sine;
                _array[11] = -sine;
                break;
            // Rotation around the green color
            case 1:
                _array[0] = _array[12] = cosine;
                _array[2] = -sine;
                _array[10] = sine;
                break;
            // Rotation around the blue color
            case 2:
                _array[0] = _array[6] = cosine;
                _array[1] = sine;
                _array[5] = -sine;
                break;
            default:
                throw new ArgumentException();
        }
    }

    /// <summary>
    /// Set this ColorMatrix to the concatenation of the two specified
    /// ColorMatrices, such that the resulting ColorMatrix has the same effect
    /// as applying matB and then applying matA. It is legal for either matA or
    /// matB to be the same ColorMatrix as this.
    /// </summary>
    public void SetConcat(ColorMatrix matA, ColorMatrix matB)
    {
        float[] tmp;

        if (matA == this || matB == this)
        {
            tmp = new float[20];
        }
        else
        {
            tmp = _array;
        }

        var a = matA._array;
        var b = matB._array;
        var index = 0;
        for (var j = 0; j < 20; j += 5)
        {
            for (var i = 0; i < 4; i++)
            {
                tmp[index++] = a[j + 0] * b[i + 0] + a[j + 1] * b[i + 5] +
                               a[j + 2] * b[i + 10] + a[j + 3] * b[i + 15];
            }
            tmp[index++] = a[j + 0] * b[4] + a[j + 1] * b[9] +
                           a[j + 2] * b[14] + a[j + 3] * b[19] +
                           a[j + 4];
        }

        if (tmp != _array)
        {
            Array.Copy(tmp, 0, _array, 0, 20);
        }
    }

    /// <summary>
    /// Concat this ColorMatrix with the specified prematrix. This is logically
    /// the same as calling setConcat(this, prematrix);
    /// </summary>
    public void PreConcat(ColorMatrix prematrix)
    {
        SetConcat(this, prematrix);
    }

    /// <summary>
    /// Concat this ColorMatrix with the specified postmatrix. This is logically
    /// the same as calling setConcat(postmatrix, this);
    /// </summary>
    public void PostConcat(ColorMatrix postmatrix)
    {
        SetConcat(postmatrix, this);
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Set the matrix to affect the saturation of colors. A value of 0 maps the
    /// color to gray-scale. 1 is identity.
    /// </summary>
    public void SetSaturation(float sat)
    {
        Reset();
        var m = _array;

        var invSat = 1 - sat;
        var R = 0.213f * invSat;
        var G = 0.715f * invSat;
        var B = 0.072f * invSat;
        m[0] = R + sat; m[1] = G; m[2] = B;
        m[5] = R; m[6] = G + sat; m[7] = B;
        m[10] = R; m[11] = G; m[12] = B + sat;
    }

    /// <summary>
    /// Set the matrix to convert RGB to YUV
    /// </summary>
    public void SetRGB2YUV()
    {
        Reset();
        var m = _array;
        // these coefficients match those in libjpeg
        m[0] = 0.299f; m[1] = 0.587f; m[2] = 0.114f;
        m[5] = -0.16874f; m[6] = -0.33126f; m[7] = 0.5f;
        m[10] = 0.5f; m[11] = -0.41869f; m[12] = -0.08131f;
    }

    /// <summary>
    /// Set the matrix to convert from YUV to RGB
    /// </summary>
    public void SetYUV2RGB()
    {
        Reset();
        var m = _array;
        // these coefficients match those in libjpeg
        m[2] = 1.402f;
        m[5] = 1; m[6] = -0.34414f; m[7] = -0.71414f;
        m[10] = 1; m[11] = 1.772f; m[12] = 0;
    }
}