#nullable enable
namespace Pix2d.Abstract.Services;

/// <summary>
/// Optional capability for platform services that can hand a plain crash-report file off to the
/// system share sheet. Android implements this; other heads can opt in later.
/// </summary>
public interface ICrashReportShareTarget
{
    void ShareCrashReportFile(string filePath, string subject);
}
