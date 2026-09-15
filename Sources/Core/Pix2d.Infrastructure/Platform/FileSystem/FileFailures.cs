#nullable enable
using System;
using System.IO;

namespace Pix2d.Abstract.Platform.FileSystem;

/// <summary>
/// Classifies the exceptions a write can fail with, so every "couldn't save / couldn't export" path
/// describes the same failure the same way.
/// </summary>
public static class FileFailures
{
    /// <summary>
    /// True when the write failed because of the <i>destination</i> — unwritable, locked, full, or simply
    /// no longer there — rather than because of what was being written.
    ///
    /// <para>The platform check is not defensive padding: on Android a destination picked through SAF
    /// fails with <c>Java.IO.IOException</c>, which derives from <c>Java.Lang.Throwable</c> and is
    /// therefore <b>not</b> a <see cref="IOException"/>. An export to an SD card that had been unmounted
    /// since the folder was picked ("No content provider: content://…", appstat 3.12.0) fell past every
    /// specific case in the export handler and came back to the user as "There's nothing to Export!" —
    /// a message about the wrong thing entirely. Matching by namespace keeps Core free of a
    /// platform-specific reference; the head assemblies are the only place those types exist.</para>
    /// </summary>
    public static bool IsDestinationFailure(Exception e) =>
        e is IOException or UnauthorizedAccessException
        || e.GetType().FullName?.StartsWith("Java.IO.", StringComparison.Ordinal) == true;
}
