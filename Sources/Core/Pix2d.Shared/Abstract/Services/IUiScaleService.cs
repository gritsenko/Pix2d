namespace Pix2d.Abstract.Services;

/// <summary>
/// Service for applying UI scale to Avalonia HostView.
/// </summary>
public interface IUiScaleService
{
    /// <summary>
    /// Apply UI scale to the main HostView.
    /// </summary>
    /// <param name="scale">Scale factor (1.0 = 100%).</param>
    void SetUiScale(double scale);
}
