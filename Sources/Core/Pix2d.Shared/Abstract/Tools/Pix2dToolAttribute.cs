#nullable enable
namespace Pix2d.Abstract.Tools;

[AttributeUsage(AttributeTargets.Class)]
public class Pix2dToolAttribute : Attribute
{
    public bool HasSettings { get; set; }

    /// <summary>
    /// if set to false it will be disabled while animation is playing
    /// </summary>
    public bool EnabledDuringAnimation { get; set; }

    /// <summary>
    /// Human-readable name of tool
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Hot key string displayed in tooltips
    /// </summary>
    public string? HotKey { get; set; }

    public string? IconData { get; set; }

    public Type? SettingsViewType { get; set; }
    public EditContextType EditContextType { get; set; }

    public string? Group { get; set; }
}