using System;
using System.IO;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Common.FileSystem;

public class NetFileSource : IFileContentSource
{
    public string Path { get; }

    public bool Exists => File.Exists(Path);

    public string Extension => System.IO.Path.GetExtension(Path);
    public string Title { get; set; }

    public Task<byte[]> GetContentAsync()
    {
        return Task.FromResult(File.ReadAllBytes(Path));
    }

    public Task SaveAsync(Stream sourceStream)
    {
        if (File.Exists(Path))
            File.Delete(Path);

        using var fileStream = File.OpenWrite(Path);
        sourceStream.CopyTo(fileStream);
        fileStream.Flush();
        fileStream.Close();

        return Task.CompletedTask;
    }

    public Task<Stream> OpenWriteAsync()
    {
        var outputFileStream = File.OpenWrite(Path);
        outputFileStream.SetLength(0);
        outputFileStream.Position = 0;
        return Task.FromResult<Stream>(outputFileStream);
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
