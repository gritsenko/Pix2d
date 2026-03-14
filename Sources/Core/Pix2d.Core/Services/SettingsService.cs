using System.Text.Json;
using Newtonsoft.Json;

namespace Pix2d.Services;

public class SettingsService(IPlatformStuffService platformStuffService) : ISettingsService {
    public const string DbName = "pix2d_settings.json";
    private string DbFullPath => System.IO.Path.Combine(platformStuffService.GetAppFolderPath(), DbName);

    private AppSettings Settings = new();

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        WriteIndented = true
    };

    private bool _isSettingsInitialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private async Task LoadJson()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_isSettingsInitialized)
                return;

            _isSettingsInitialized = true;
            if (!File.Exists(DbFullPath))
            {
                Settings = new AppSettings();
                return;
            }

            try
            {
                var json = File.ReadAllText(DbFullPath);

                if (json.Contains("\"$type\"") || json.Contains("\"$values\""))
                {
                    var oldSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (oldSettings != null)
                    {
                        Settings = oldSettings;
                        Save();
                    }
                    return;
                }

                Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions) ?? new AppSettings();
            }
            catch(Exception ex)
            {
                Logger.LogException(ex);
                Settings = new AppSettings();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public T? Get<T>(string key) {
        try {
            EnsureSettingsInitialized();

            var property = typeof(AppSettings).GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property != null)
            {
                var value = property.GetValue(Settings);
                if (value is T typedValue)
                    return typedValue;
                if (value != null)
                    return (T)Convert.ChangeType(value, typeof(T));
            }
        }
        catch (Exception ex) {
            Logger.LogException(ex);
        }

        return default;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        try
        {
            EnsureSettingsInitialized();

            var property = typeof(AppSettings).GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property != null)
            {
                var propValue = property.GetValue(Settings);
                if (propValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                if (propValue != null)
                {
                    value = (T)Convert.ChangeType(propValue, typeof(T));
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        value = default;
        return false;
    }

    private void EnsureSettingsInitialized()
    {
        if (!_isSettingsInitialized)
            LoadJson().GetAwaiter().GetResult();
    }

    private void Save()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Settings, _serializerOptions);

        var dir = platformStuffService.GetAppFolderPath();
        if (!Directory.Exists(dir)) 
            Directory.CreateDirectory(dir);

        var tempFile = DbFullPath + ".tmp";
        var backupFile = DbFullPath + ".bak";

        try
        {
            File.WriteAllText(tempFile, json);
            
            if (File.Exists(DbFullPath))
                File.Replace(tempFile, DbFullPath, backupFile);
            else
                File.Move(tempFile, DbFullPath);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            Logger.LogException(ex);
            throw;
        }
        finally
        {
            if (File.Exists(backupFile))
                File.Delete(backupFile);
        }
    }

    public void Set<T>(string key, T? value)
    {
        EnsureSettingsInitialized();
        _semaphore.Wait();
        try
        {
            var property = typeof(AppSettings).GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(Settings, value);
                Save();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
