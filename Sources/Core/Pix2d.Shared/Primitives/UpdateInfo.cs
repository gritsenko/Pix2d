#nullable enable
using System;

namespace Pix2d.Primitives;

/// <summary>
/// Describes a newer release discovered by <see cref="Pix2d.Abstract.Services.IUpdateService"/>.
/// Populated from the GitHub "latest release" API.
/// </summary>
public sealed record UpdateInfo(
    Version Version,
    string Name,
    string ReleaseNotes,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    string? DownloadUrl = null);
