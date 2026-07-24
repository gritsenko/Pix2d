using System;
using System.IO;
using Pix2d.Abstract.Services;

namespace Pix2d.Services;

public class OperationDiskCacheService : IOperationDiskCacheService
{
    private readonly string _sessionCacheDir;

    public OperationDiskCacheService()
    {
        var tempPath = Path.GetTempPath();
        var sessionId = Guid.NewGuid().ToString("N");
        _sessionCacheDir = Path.Combine(tempPath, "Pix2d", "OperationCache", sessionId);

        EnsureCacheDir();
    }

    /// <summary>
    /// Recreates the session cache directory if it went missing. The undo cache lives under the OS temp
    /// folder, which Windows Storage Sense / cleanmgr, /tmp reapers and antivirus can wipe while the app is
    /// running — every subsequent eviction then died with DirectoryNotFoundException from WriteAllBytes.
    /// </summary>
    private void EnsureCacheDir()
    {
        if (!Directory.Exists(_sessionCacheDir))
        {
            Directory.CreateDirectory(_sessionCacheDir);
        }
    }

    public void SaveData(string key, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        if (data == null)
            throw new ArgumentNullException(nameof(data));

        EnsureCacheDir();

        var filePath = Path.Combine(_sessionCacheDir, SanitizeKey(key));
        File.WriteAllBytes(filePath, data);
    }

    public byte[] LoadData(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        var filePath = Path.Combine(_sessionCacheDir, SanitizeKey(key));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Cache entry not found: {key}", filePath);

        return File.ReadAllBytes(filePath);
    }

    public void DeleteData(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        var filePath = Path.Combine(_sessionCacheDir, SanitizeKey(key));
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public void ClearAll()
    {
        if (Directory.Exists(_sessionCacheDir))
        {
            Directory.Delete(_sessionCacheDir, recursive: true);
        }

        EnsureCacheDir();
    }

    private static string SanitizeKey(string key)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = key;
        
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        
        return sanitized;
    }
}
