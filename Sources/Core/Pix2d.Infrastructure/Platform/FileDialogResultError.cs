#nullable enable
namespace Pix2d.Abstract.Platform;

public enum FileDialogResultError
{
    NoFileSelected,
    FileSourceNotCreated,

    /// <summary>
    /// The platform file dialog itself failed to run (not a user cancel). Callers should treat it like
    /// "no file" — <see cref="Pix2d.Abstract.Services.IFileService"/> has already told the user.
    /// </summary>
    DialogFailed
}