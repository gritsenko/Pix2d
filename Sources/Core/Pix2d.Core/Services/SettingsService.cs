using System.Text.Json;
using System.Threading;

namespace Pix2d.Services;

public class SettingsService(IPlatformStuffService platformStuffService) : ISettingsService {
    public const string DbName = "pix2d_settings.json";
    private string DbFullPath => System.IO.Path.Combine(platformStuffService.GetAppFolderPath(), DbName);

    private Dictionary<string, JsonElement> Settings = new();

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
                Settings = new Dictionary<string, JsonElement>();
                return;
            }

            try
            {
                var json = File.ReadAllText(DbFullPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _serializerOptions);
                if (settings != null) 
                    Settings = settings;
            }
            catch(Exception ex)
            {
                Logger.LogException(ex);
                Settings = new Dictionary<string, JsonElement>();
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

            if (Settings.TryGetValue(key, out var jsonElement))
            {
                return jsonElement.Deserialize<T>(_serializerOptions);
            }
        }
        catch (Exception ex) {
            Logger.LogException(ex);
        }

        return default;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        EnsureSettingsInitialized();
        if (Settings.TryGetValue(key, out var jsonElement))
        {
            try
            {
                value = jsonElement.Deserialize<T>(_serializerOptions);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        value = default;
        return false;
    }

    private void EnsureSettingsInitialized()
    {
        //ensure the settings were loaded
        if (!_isSettingsInitialized)
            LoadJson().GetAwaiter().GetResult();
    }

    public void Set<T>(string key, T? value)
    {
        EnsureSettingsInitialized();
        _semaphore.Wait();
        try
        {
            Settings[key] = JsonSerializer.SerializeToElement(value, _serializerOptions);
            
            var json = JsonSerializer.Serialize(Settings, _serializerOptions);

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
        finally
        {
            _semaphore.Release();
        }
    }

    

    
}