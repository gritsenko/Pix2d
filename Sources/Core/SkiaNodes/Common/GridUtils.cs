namespace SkiaNodes;

public static class GridUtils
{
    /// <summary>
    /// Calculate an adaptive grid step size that remains visually
    /// consistent across different zoom levels.
    /// </summary>
    /// <param name="zoom">Current viewport zoom</param>
    /// <param name="targetScreenSize">Desired size of the grid cell on screen in pixels (default is 24px).</param>
    /// <param name="minStep">Minimum allowable step size in local coordinates (default is 1).</param>
    /// <returns>Calculated step size in local coordinates.</returns>
    public static float CalculateAdaptiveStep(float zoom, float targetScreenSize = 24f, float minStep = 1f)
    {
        float idealLocalSize = targetScreenSize / zoom;
        float exponent = MathF.Round(MathF.Log2(idealLocalSize));
        float effectiveSize = MathF.Pow(2, exponent);
        return Math.Max(effectiveSize, minStep);
    }
}