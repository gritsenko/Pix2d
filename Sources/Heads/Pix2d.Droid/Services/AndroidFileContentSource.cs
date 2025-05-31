using Android.Content;
using Android.Database;
using Android.Net;
using Android.Provider;
using Android.Webkit; 
using Pix2d.Droid;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using Uri = Android.Net.Uri;

public class AndroidFileContentSource : IFileContentSource
{
    private readonly Uri _contentUri;

    // Path для content:// URI не является файловым путем, это строковое представление URI
    public string Path => _contentUri.ToString();

    // Exists и LastModified для content:// URI нужно получать через ContentResolver.Query
    public bool Exists { get; } // Получается в конструкторе
    public DateTime LastModified { get; } // Получается в конструкторе (если доступно)
    public long Size { get; } // Добавим размер файла, если доступен

    // Расширение лучше получать из имени файла или MIME типа через ContentResolver
    public string Extension { get; }

    // Название файла для отображения
    public string Title { get; set; }


    // Используем статический инстанс MainActivity для получения ContentResolver
    // Это менее чисто, чем передавать ContentResolver напрямую, но соответствует вашей структуре
    private ContentResolver GetContentResolver()
    {
        // Используем вспомогательный метод из MainActivity.Instance
        // Это гарантирует, что у нас есть ContentResolver из активной Activity
        return MainActivity.Instance.GetContentResolverHelper();
    }

    // Конструктор
    // Принимает расширение по умолчанию (если не удалось определить из URI/имени), и сам URI
    public AndroidFileContentSource(Uri contentUri, string? defaultExtension = null)
    {
        _contentUri = contentUri ?? throw new ArgumentNullException(nameof(contentUri));

        string displayName = _contentUri.LastPathSegment ?? "Untitled"; // Название по умолчанию из последнего сегмента URI
        DateTime lastModified = DateTime.MinValue; // Дата изменения по умолчанию
        long size = -1; // Размер по умолчанию
        string determinedExtension = defaultExtension; // Расширение по умолчанию
        bool exists = false; // Существование по умолчанию

        var resolver = GetContentResolver();

        // Попытка получить метаданные с помощью ContentResolver.Query
        // Работает как для DocumentsContract URI (из SAF) так и для OpenableColumns URI
        if (resolver != null && contentUri.Scheme == ContentResolver.SchemeContent)
        {
            ICursor? cursor = null;
            try
            {
                // Запрашиваем стандартные колонки для OpenableColumns и DocumentsContract
                string[] projection = {
                    OpenableColumns.DisplayName,
                    OpenableColumns.Size,
                    // DocumentsContract.Document.ColumnLastModified // Этот столбец доступен для DocumentsContract URI
                };

                cursor = resolver.Query(contentUri, projection, null, null, null);

                if (cursor != null && cursor.MoveToFirst())
                {
                    exists = true; // Если запрос вернул результат, считаем, что файл существует

                    int displayNameIndex = cursor.GetColumnIndex(OpenableColumns.DisplayName);
                    if (displayNameIndex != -1)
                    {
                        displayName = cursor.GetString(displayNameIndex);
                    }

                    int sizeIndex = cursor.GetColumnIndex(OpenableColumns.Size);
                    if (sizeIndex != -1)
                    {
                        size = cursor.GetLong(sizeIndex);
                    }

                    // Попытка получить дату изменения (может не поддерживаться всеми провайдерами)
                    // Для SAF URI (content://...) используем DocumentsContract.Document.ColumnLastModified
                    // Для других content:// URI это может не работать или требовать другого подхода
                    try
                    {
                        int lastModifiedIndex = cursor.GetColumnIndex(DocumentsContract.Document.ColumnLastModified);
                        if (lastModifiedIndex != -1 && !cursor.IsNull(lastModifiedIndex))
                        {
                            long timestamp = cursor.GetLong(lastModifiedIndex); // Unix timestamp in milliseconds
                            lastModified = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                        }
                    }
                    catch (Exception)
                    {
                        // Пропускаем ошибку, если колонка не найдена или тип данных не соответствует
                        System.Diagnostics.Debug.WriteLine($"Could not get LastModified for URI: {contentUri}");
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying URI metadata {contentUri}: {ex.Message}");
                // Оставляем значения по умолчанию
            }
            finally
            {
                cursor?.Close();
            }
        }
        else if (contentUri.Scheme == ContentResolver.SchemeFile)
        {
            // Если это file:// URI, используем обычный System.IO.FileInfo
            try
            {
                var filePath = contentUri.Path; // Для file:// используем Path или LocalPath
                if (filePath != null)
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists)
                    {
                        exists = true;
                        displayName = fileInfo.Name;
                        size = fileInfo.Length;
                        lastModified = fileInfo.LastWriteTime;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling file:// URI {contentUri}: {ex.Message}");
            }
        }


        Title = displayName;
        Size = size;
        LastModified = lastModified;
        Exists = exists;
        // Попытаться определить расширение из MIME типа или имени файла
        determinedExtension = System.IO.Path.GetExtension(Title);
        if (string.IsNullOrEmpty(determinedExtension))
        {
            try
            {
                // Попытаться получить MIME тип и из него определить расширение
                string? mimeType = resolver?.GetType(contentUri);
                if (mimeType != null)
                {
                    string? extensionFromMime = MimeTypeMap.Singleton?.GetExtensionFromMimeType(mimeType);
                    if (extensionFromMime != null)
                    {
                        determinedExtension = "." + extensionFromMime;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MIME type for URI {contentUri}: {ex.Message}");
            }
        }
        Extension = string.IsNullOrEmpty(determinedExtension) ? defaultExtension : determinedExtension;

        System.Diagnostics.Debug.WriteLine($"Created AndroidFileContentSource for URI: {_contentUri}, Title: {Title}, Extension: {Extension}, Exists: {Exists}, Size: {Size}");

        // TryGetFileNameFromUri теперь не нужен, так как логика перенесена в конструктор
        // CreateFromContentUri тоже не нужен, так как конструктор справляется
    }

    // Реализация интерфейса IFileContentSource

    // OpenRead теперь просто открывает поток. SecurityException должна обрабатываться вызывающим кодом.
    public async Task<Stream> OpenRead()
    {
        System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenRead: Attempting to open stream for {_contentUri}");
        try
        {
            // Permission Denial (SecurityException) происходит именно здесь
            var inputStream = GetContentResolver().OpenInputStream(_contentUri);
            if (inputStream == null)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenRead: OpenInputStream returned null for {_contentUri}");
                throw new FileNotFoundException($"Не удалось получить входной поток для URI: {_contentUri}. Файл не найден или недоступен.");
            }
            // Если файл небольшой, можно сразу прочитать в MemoryStream.
            // Для больших файлов лучше вернуть inputStream напрямую, но тогда вызывающий код
            // должен его правильно использовать и закрыть. Ваша текущая структура предполагает
            // чтение в MemoryStream.
            var destStream = new MemoryStream();
            await inputStream.CopyToAsync(destStream);
            destStream.Seek(0, SeekOrigin.Begin);
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenRead: Successfully read stream for {_contentUri}");
            return destStream;
        }
        catch (Exception ex) // Ловим возможные ошибки при работе с потоком или SecurityException
        {
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenRead: Error opening/reading stream for {_contentUri}: {ex.Message}");
            // Перебрасываем исключение, чтобы вызывающий код (MainActivity или сервис) мог его обработать
            throw;
        }
    }

    // SaveAsync - сохранение данных из потока в файл по URI
    public async Task SaveAsync(Stream sourceStream)
    {
        System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.SaveAsync: Attempting to save stream to {_contentUri}");
        try
        {
            // Permission Denial (SecurityException) может произойти здесь, если нет разрешения на запись
            // OpenOutputStream с "w", "wt", "wa", "rw"
            using var outputStream = GetContentResolver().OpenOutputStream(_contentUri, "w"); // "w" - перезаписать
            if (outputStream == null)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.SaveAsync: OpenOutputStream returned null for {_contentUri}");
                throw new IOException($"Не удалось получить выходной поток для URI: {_contentUri}. Нет доступа или файл не может быть записан.");
            }

            // Убедимся, что исходный поток находится в начале
            if (sourceStream.CanSeek)
            {
                sourceStream.Seek(0, SeekOrigin.Begin);
            }
            else
            {
                // Если поток не поддерживает Seek, и нужно начать с начала,
                // возможно, придется прочитать его в буфер или MemoryStream сначала.
                System.Diagnostics.Debug.WriteLine("Warning: sourceStream does not support seeking.");
                // В этом случае, если sourceStream уже частично прочитан, SaveAsync запишет оставшуюся часть.
            }


            await sourceStream.CopyToAsync(outputStream); // Эффективное копирование
            // CopyToAsync вызывает Flush в конце. `using` позаботится о Close/Dispose.
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.SaveAsync: Successfully saved stream to {_contentUri}");
        }
        catch (Exception ex) // Ловим возможные ошибки при записи или SecurityException
        {
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.SaveAsync: Error saving stream to {_contentUri}: {ex.Message}");
            throw; // Перебрасываем исключение
        }
    }

    // OpenWriteAsync - получение потока для записи в файл по URI
    public async Task<Stream> OpenWriteAsync()
    {
        System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenWriteAsync: Attempting to open stream for writing to {_contentUri}");
        try
        {
            // Permission Denial (SecurityException) может произойти здесь
            // OpenOutputStream с "w" или "rw"
            var outputStream = GetContentResolver().OpenOutputStream(_contentUri, "w"); // Открыть для перезаписи
            if (outputStream == null)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenWriteAsync: OpenOutputStream returned null for {_contentUri}");
                throw new IOException($"Не удалось получить выходной поток для записи по URI: {_contentUri}. Нет доступа или файл не может быть создан/перезаписан.");
            }
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenWriteAsync: Successfully opened stream for writing to {_contentUri}");
            return outputStream;
        }
        catch (Exception ex) // Ловим возможные ошибки или SecurityException
        {
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.OpenWriteAsync: Error opening stream for writing to {_contentUri}: {ex.Message}");
            throw; // Перебрасываем исключение
        }
    }


    // Delete - удаление файла по URI
    public void Delete()
    {
        System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: Attempting to delete {_contentUri}");
        var resolver = GetContentResolver();
        if (resolver == null)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: ContentResolver is null.");
            throw new InvalidOperationException("ContentResolver не доступен для удаления.");
        }

        try
        {
            // ContentResolver.Delete работает для провайдеров, которые поддерживают удаление по URI.
            // DocumentsContract провайдеры (из SAF) обычно поддерживают.
            // Возвращает количество удаленных строк (файлов).
            int deletedRows = resolver.Delete(_contentUri, null, null);

            if (deletedRows > 0)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: Successfully deleted {_contentUri} ({deletedRows} rows).");
                // Можно добавить логику для очистки состояния объекта, если он представляет удаленный файл
            }
            else
            {
                // Удаление могло не произойти, если файл не найден, нет разрешения,
                // или провайдер не поддерживает удаление через ContentResolver.Delete
                System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: Deletion of {_contentUri} failed or file not found (returned {deletedRows} rows deleted).");
                // Можно выбросить исключение, если удаление обязательно должно было произойти
                // throw new IOException($"Не удалось удалить файл: {_contentUri}. Возможно, нет разрешения или файл не найден."); // Пример
            }
        }
        catch (Java.Lang.SecurityException ex)
        {
            // Обработка специфической ошибки отсутствия разрешения на удаление
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: Security Exception deleting {_contentUri}: {ex.Message}");
            throw new IOException($"Отказано в разрешении на удаление файла: {_contentUri}", ex); // Оборачиваем и перебрасываем
        }
        catch (Exception ex)
        {
            // Обработка других ошибок при удалении
            System.Diagnostics.Debug.WriteLine($"AndroidFileContentSource.Delete: Error deleting {_contentUri}: {ex.Message}");
            throw new IOException($"Произошла ошибка при удалении файла: {_contentUri}", ex); // Оборачиваем и перебрасываем
        }
    }


    // Методы для работы с текстом (если нужны)
    public async Task SaveAsync(string textContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));
        await SaveAsync(stream);
    }

    public async Task<string> ReadTextAsync()
    {
        using var stream = await OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}