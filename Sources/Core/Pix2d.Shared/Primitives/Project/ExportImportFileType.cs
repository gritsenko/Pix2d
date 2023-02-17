namespace Pix2d.Abstract;

public class ExportImportProjectType
{
    public static ExportImportProjectType Pix2d = new(".pix2d");
    public static ExportImportProjectType Pix2dFolder = new();

    public static ExportImportProjectType PixelArtStudio = new(".pxm");
    public static ExportImportProjectType Png = new(".png");
    public static ExportImportProjectType Gif = new(".gif");
    public static ExportImportProjectType Jpeg = new(".jpg");

    public string FileExtension { get; }
    public bool IsDirectoryProject { get; }

    public ExportImportProjectType(string fileExtension)
    {
        FileExtension = fileExtension;
    }
    public ExportImportProjectType()
    {
        IsDirectoryProject = true;
    }

    public static string[] GetSupportedImportFileExtensions()
    {
        return new[]
        {
            Pix2d.FileExtension,
            PixelArtStudio.FileExtension,
            Png.FileExtension,
            Gif.FileExtension,
            Jpeg.FileExtension,
        };
    }
}