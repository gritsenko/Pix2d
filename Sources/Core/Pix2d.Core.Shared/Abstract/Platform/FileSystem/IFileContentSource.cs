using System;
using System.IO;
using System.Threading.Tasks;

namespace Pix2d.Abstract.Platform.FileSystem
{
    public interface IFileContentSource
    {
        string Path { get; }
        bool Exists { get; }

        /// <summary>
        /// file extension in .xxx format (include lead dot)
        /// </summary>
        string Extension { get; }

        string Title { get; set; }

        Task<byte[]> GetContentAsync();
        Task SaveAsync(Stream sourceStream);

//        void SaveAsync(string textContent);

        Task SaveAsync(string textContent);
        Task<Stream> OpenRead();
        Task<Stream> OpenWriteAsync();
        
        void Delete();
    }
}