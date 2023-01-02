using Android.Provider;
using Java.IO;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.AvaloniaAndroid;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Uri = Android.Net.Uri;

namespace Pix2d.Services
{
    public class AndroidFileContentSource : IFileContentSource
    {
        private readonly Uri _contentUri;
        public string Path { get; }
        public bool Exists { get; }
        public string Extension { get; }
        public string Title { get; set; }

        public AndroidFileContentSource(string extension, Android.Net.Uri contentUri)
        {
            _contentUri = contentUri;
            Extension = extension;
            Path = contentUri.ToString();
            Title = Path;
            Exists = true;
        }


        public Task<byte[]> GetContentAsync()
        {
            throw new NotImplementedException();
        }

        public async Task SaveAsync(Stream sourceStream)
        {
            // Buffer size can be "tuned" to enhance read/write performance
            await Task.Run(() =>
            {
                var buffer = new byte[1024];
                //using (var pFD = MainActivity.Instance.ContentResolver.OpenFileDescriptor(_contentUri, "w"))
                //using (var outputSteam = new FileOutputStream(pFD.FileDescriptor))
                using (var outputSteam = MainActivity.Instance.ContentResolver.OpenOutputStream(_contentUri))
                using (sourceStream)
                {

                    sourceStream.Seek(0, SeekOrigin.Begin);
                    while (sourceStream.CanRead && sourceStream.Position < sourceStream.Length)
                    {
                        var readCount = sourceStream.Read(buffer, 0, buffer.Length);
                        outputSteam.Write(buffer, 0, readCount);
                    }

                    outputSteam.Flush();
                    outputSteam.Close();
                }

            });
        }

        public void Save(string textContent)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync(string textContent)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenRead()
        {
            var buffer = new byte[1024];
            var cr = MainActivity.Instance.ContentResolver;
            if (cr == null)
                Task.FromResult<Stream>(default);

            using var pFd = cr.OpenFileDescriptor(_contentUri, "r");
            using var inputStream = new FileInputStream(pFd.FileDescriptor);
            var destStream = new MemoryStream();

            while (destStream.CanWrite && inputStream.Available() > 0)
            {
                var readCount = inputStream.Read(buffer, 0, buffer.Length);
                destStream.Write(buffer, 0, readCount);

                if (readCount < buffer.Length)
                    break;
            }
            inputStream.Close();
            destStream.Seek(0, SeekOrigin.Begin);

            return Task.FromResult<Stream>(destStream);
        }

        public async Task<Stream> OpenWriteAsync()
        {
            var outputSteam = MainActivity.Instance.ContentResolver.OpenOutputStream(_contentUri);
            return outputSteam;
        }

        public Task SaveCompressedPng(Stream sourcePngStream)
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            System.IO.File.Delete(Path);
            Debugger.Break();
        }

        public static AndroidFileContentSource CreateFromContentUri(Android.Net.Uri uri, string dataString)
        {
            var displayName = dataString;
            if (AndroidFileContentSource.TryGetFileNameFromUri(uri, out var fileName))
            {
                // var resolver = MainActivity.Instance.ContentResolver;
                // resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

                displayName = fileName;
                var type = System.IO.Path.GetExtension(fileName);

                var fcs = new AndroidFileContentSource(type, uri);
                fcs.Title = displayName;
                return fcs;
            }

            return null;
        }

        internal static bool TryGetFileNameFromUri(Android.Net.Uri uri, out string displayName)
        {
            var result = false;
            displayName = default;

            var resolver = MainActivity.Instance?.ContentResolver;

            if (resolver != null && uri != Uri.Empty && uri != null
                && uri.ToString().StartsWith("content://"))
            {
                Android.Database.ICursor cursor = null;
                try
                {
                    cursor = resolver.Query(uri, null, null, null, null);
                    if (cursor != null && cursor.MoveToFirst())
                    {
                        displayName = cursor.GetString(cursor.GetColumnIndex(OpenableColumns.DisplayName));
                        result = true;
                    }
                }
                finally
                {
                    cursor.Close();
                }
            }

            return result;
        }

    }
}