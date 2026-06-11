#nullable enable
using Newtonsoft.Json;

namespace Pix2d.Project.AutoSave;

/// <summary>
/// Records which session folders belong to the tabs that were open in the last run,
/// in tab order, plus which one was active. Written atomically to
/// <c>&lt;sessionsRoot&gt;/workspace.json</c> after every commit / tab change; read once at
/// startup to restore all tabs. A missing or corrupt file degrades gracefully to the
/// single most-recent-session recovery.
/// </summary>
public sealed class WorkspaceManifest
{
    [JsonProperty("v")] public int FormatVersion { get; set; } = 1;

    [JsonProperty("tabs")] public List<WorkspaceTab> Tabs { get; set; } = [];

    /// <summary>Index of the active tab within <see cref="Tabs"/>.</summary>
    [JsonProperty("active")] public int ActiveIndex { get; set; }

    [JsonProperty("at")] public DateTime SavedAtUtc { get; set; }
}

public sealed class WorkspaceTab
{
    /// <summary>Session folder name under <c>&lt;sessionsRoot&gt;/active/</c>.</summary>
    [JsonProperty("sid")] public string SessionId { get; set; } = "";

    /// <summary>Original on-disk project path, when the tab was backed by a file.</summary>
    [JsonProperty("src")] public string? SourceProjectPath { get; set; }
}
