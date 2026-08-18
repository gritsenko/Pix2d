using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Primitives;

public class MruRecord
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;

    public MruRecord()
    {
    }

    public MruRecord(IFileContentSource file)
    {
        Path = CanonicalizePath(file.Path);
        Name = file.Title!;
    }

    /// <summary>
    /// Key that answers "is this the same file", used for MRU de-duplication.
    ///
    /// <para>The recent-projects list showed one project twice because the same file reached the list under
    /// two spellings — <c>C:/Users/…/Downloads/new_project.pix2d</c> (a path that came in through a URI, so
    /// with forward slashes) and <c>C:\Users\…\Downloads\new_project.pix2d</c> — and the plain string
    /// compare in <c>AddToMru</c> treated them as different files.</para>
    ///
    /// <para>This is deliberately pure string work: <see cref="System.IO.Path.GetFullPath(string)"/> would
    /// also collapse <c>..</c> segments, but it mangles the URI-shaped paths that are exactly the ones
    /// needing normalization — on Windows it turns <c>/C:/Users/igor</c> into <c>C:\C:\Users\igor</c>. An
    /// entry with no filesystem path at all (Android SAF <c>content://</c>, a browser handle) is keyed by
    /// its raw string.</para>
    /// </summary>
    public string GetComparisonKey() => NormalizePath(Path);

    public static string NormalizePath(string? path)
    {
        var canonical = CanonicalizePath(path);
        return PathsAreCaseSensitive ? canonical : canonical.ToLowerInvariant();
    }

    /// <summary>
    /// The spelling of a path that gets *stored and shown*: same separator unification as
    /// <see cref="NormalizePath"/> but with the original casing, since this one reaches the user (the
    /// recent-projects tooltip) and the filesystem. Keeping it separate from the comparison key is the point
    /// — a case-folded path would be wrong to display and wrong to hand to a case-sensitive filesystem.
    /// </summary>
    public static string CanonicalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var canonical = path.Trim();

        // A source with no filesystem path (Android SAF content:// URI, browser handle) is left verbatim:
        // its separators are part of the URI, not of a path this platform owns.
        if (canonical.Contains("://"))
            return canonical;

        return canonical
            .Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar);
    }

    private static bool PathsAreCaseSensitive => OperatingSystem.IsLinux() || OperatingSystem.IsAndroid();

    protected bool Equals(MruRecord other) => Name == other.Name && Path == other.Path;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MruRecord)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Path != null ? Path.GetHashCode() : 0);
        }
    }
}
