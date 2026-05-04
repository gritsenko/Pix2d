#nullable enable
using Newtonsoft.Json;

namespace Pix2d.Project.AutoSave;

/// <summary>
/// Atomic commit record for a session work folder. The manifest file is the
/// "commit point" — its presence (and the highest <see cref="Revision"/> inside it)
/// guarantees that the referenced <c>scene.json</c> + all frame PNGs were fully written.
///
/// Renaming <c>manifest.json.tmp</c> to <c>manifest.json</c> is the single atomic step
/// that publishes a new revision; readers see either the old manifest or the new one,
/// never a half-written one.
/// </summary>
public sealed class SessionManifest
{
    [JsonProperty("v")] public int FormatVersion { get; set; } = 1;

    [JsonProperty("sid")] public string SessionId { get; set; } = "";

    /// <summary>Monotonic counter incremented on every successful commit.</summary>
    [JsonProperty("rev")] public long Revision { get; set; }

    /// <summary>UTC timestamp of the commit.</summary>
    [JsonProperty("at")] public DateTime CommittedAtUtc { get; set; }

    /// <summary>Original on-disk project path (if the session was loaded from a file).</summary>
    [JsonProperty("src")] public string? SourceProjectPath { get; set; }

    /// <summary>List of all frame keys that the current scene.json references.</summary>
    [JsonProperty("frames")] public List<string> FrameKeys { get; set; } = [];

    /// <summary>Path of the scene snapshot relative to the session folder.</summary>
    [JsonProperty("scene")] public string SceneFile { get; set; } = "scene.json";

    /// <summary>Optional thumbnail file relative to the session folder.</summary>
    [JsonProperty("thumb")] public string? ThumbnailFile { get; set; }
}
