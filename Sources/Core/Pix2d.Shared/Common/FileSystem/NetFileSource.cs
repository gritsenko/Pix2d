using System;
using System.IO;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

public class NetFileSource : IFileContentSource
{
    public string Path { get; }

    public bool Exists => File.Exists(Path);
    public DateTime LastModified => File.GetLastWriteTime(Path);

    public string Extension => System.IO.Path.GetExtension(Path);
    public string Title { get; set; }

    public Task<byte[]> GetContentAsync()
    {
        return Task.FromResult(File.ReadAllBytes(Path));
    }

    public Task SaveAsync(Stream sourceStream)
    {
        PrepareForOverwrite();

        // FileMode.Create truncates in place. The previous delete-then-OpenWrite pair needed no delete to
        // begin with, and File.Delete is *stricter* than writing: it refuses outright on a read-only file.
        using var fileStream = new FileStream(Path, FileMode.Create, FileAccess.Write);
        sourceStream.CopyTo(fileStream);
        fileStream.Flush();
        fileStream.Close();

        return Task.CompletedTask;
    }

    public Task<Stream> OpenWriteAsync()
    {
        PrepareForOverwrite();

        var outputFileStream = File.OpenWrite(Path);
        outputFileStream.SetLength(0);
        outputFileStream.Position = 0;
        return Task.FromResult<Stream>(outputFileStream);
    }

    /// <summary>
    /// Clears the read-only attribute so an overwrite the user has already asked for can proceed.
    ///
    /// <para>Windows marks files extracted from a <c>.zip</c> read-only, and both paths that reach here —
    /// export to an existing file and Ctrl+S on a PNG-backed project (which writes back to the source
    /// image) — then failed with <c>UnauthorizedAccessException</c> on a file the user had just opened
    /// from an archive: appstat, 3.11.3, `…\Temp\…\VanillaDefault 1.21.7.zip…\ladder.png`. Overwriting is
    /// the whole point of the operation and it is already confirmed upstream (export asks, Ctrl+S means
    /// save), so the read-only flag has nothing left to protect here. A genuine permission problem
    /// (ACLs, another process holding the file) still surfaces as before.</para>
    /// </summary>
    private void PrepareForOverwrite()
    {
        if (!File.Exists(Path))
            return;

        try
        {
            var attributes = File.GetAttributes(Path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(Path, attributes & ~FileAttributes.ReadOnly);
        }
        catch (Exception)
        {
            // Best effort — if the attribute cannot be cleared, let the write itself report the real error.
        }
    }

    public Task SaveCompressedPng(Stream sourcePngStream)
    {
        throw new NotImplementedException();
        //var pc = new PNGCompression.PNGCompressor();
        //pc.PNGToolPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\Utils";
        //var rawPath = Path + "_raw";

        //using (var fileStream = File.OpenWrite(rawPath))
        //{
        //    sourcePngStream.CopyTo(fileStream);
        //    fileStream.Flush();
        //    fileStream.Close();
        //}

        //pc.CompressImageLossy(rawPath, Path, new PNGCompression.LossyInputSettings("", 50, 80, 1));

        //File.Delete(rawPath);
    }

    public void Delete()
    {
        File.Delete(Path);
    }

    public void Save(string textContent)
    {
        File.WriteAllText(Path, textContent);
    }

    public Task SaveAsync(string textContent)
    {
        return Task.Run(() => { Save(textContent); });
    }

    public Task<Stream> OpenRead()
    {
        return Task.FromResult<Stream>(File.OpenRead(Path));
    }

    public NetFileSource(string path)
    {
        Path = path;
        Title = System.IO.Path.GetFileName(Path);
    }
}