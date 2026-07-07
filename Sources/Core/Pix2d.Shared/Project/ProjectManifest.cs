using Newtonsoft.Json;

namespace Pix2d.Project;

/// <summary>
/// The <c>manifest.json</c> entry inside a <c>.pix2d</c> archive. Introduced by format hardening
/// (roadmap H1.2) as the anchor for versioned loading: it records which <see cref="FormatVersion"/>
/// the archive's <c>project.json</c> was written against, so the migration pipeline can bring older
/// documents up to the current schema on open.
///
/// Archives written before this entry existed simply have no <c>manifest.json</c>; the unpacker
/// treats their absence as the baseline version (see <see cref="ProjectFormat"/>).
/// </summary>
public sealed class ProjectManifest
{
    // Baseline default: a manifest that somehow lacks this field is read as the baseline version.
    // The writer always sets it explicitly to ProjectFormat.CurrentVersion.
    [JsonProperty("formatVersion")] public int FormatVersion { get; set; } = 1;
}
