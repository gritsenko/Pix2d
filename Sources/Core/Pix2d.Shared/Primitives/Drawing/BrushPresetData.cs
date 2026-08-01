namespace Pix2d.Primitives.Drawing;

/// <summary>
/// Serializable form of a <see cref="BrushSettings"/> preset — the shape stored in
/// <c>AppSettings.UserBrushPresets</c>.
///
/// <para>A live <see cref="BrushSettings"/> cannot be persisted directly: its <see cref="BrushSettings.Brush"/>
/// is a reference to one of the shared <c>IPixelBrush</c> singletons owned by <c>DrawingService</c>. This DTO
/// replaces that reference with a <b>stable string key</b> (see <c>BrushKeys</c>) so a preset survives a brush
/// class being renamed or moved — the same reason node types persist through <c>NodeTypeRegistry</c> rather
/// than their CLR names.</para>
///
/// <para>Plain get/set scalar properties only: this round-trips through the reflection-based
/// <c>SettingsService</c> (System.Text.Json) on desktop/Android and through per-key localStorage JSON on the
/// browser head, and must stay trim-safe on WASM.</para>
/// </summary>
public class BrushPresetData
{
    /// <summary>Stable brush-type key (<c>square</c>, <c>circle</c>, <c>spray</c>, <c>marker</c>).</summary>
    public string Brush { get; set; } = "";

    public float Scale { get; set; } = 1;
    public float Opacity { get; set; } = 1;
    public float Spacing { get; set; } = 1;
    public bool PressureAffectsSize { get; set; }
    public bool PressureAffectsOpacity { get; set; }
}
