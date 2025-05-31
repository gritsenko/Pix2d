#nullable enable
using Newtonsoft.Json;

namespace Pix2d.Services;

public class SettingsService(IPlatformStuffService platformStuffService) : ISettingsService {
    public const string DbName = "pix2d_settings.json";
    private string DbFullPath => System.IO.Path.Combine(platformStuffService.GetAppFolderPath(), DbName);

    public Dictionary<string, object?> Settings { get; set; } = new();

    private readonly JsonSerializerSettings _serializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
    };

    private bool _isSettingsInitialized;

    private void LoadJson()
    {
        _isSettingsInitialized = true;
        if (!File.Exists(DbFullPath))
        {
            Settings = new Dictionary<string, object?>();
            SaveJson();
            return;
        }

        try
        {
            var json = File.ReadAllText(DbFullPath);
            var settings = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json, _serializerSettings);
            if (settings != null) 
                Settings = settings;
        }
        catch(Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public T? Get<T>(string key) {
        try {
            EnsureSettingsInitialized();

            if (Settings.TryGetValue(key, out var value))
            {
                if(value is T tValue)
                    return tValue;
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
        if (Settings.TryGetValue(key, out var storedValue))
        {
            if (storedValue is T tValue)
            {
                value = tValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    private void EnsureSettingsInitialized()
    {
        //ensure the settings were loaded
        if (!_isSettingsInitialized)
            LoadJson();
    }

    public void Set<T>(string key, T? value)
    {
        EnsureSettingsInitialized();
        Settings[key] = value;
        SaveJson();
    }

    private void SaveJson()
    {
        var json = JsonConvert.SerializeObject(Settings, _serializerSettings);

        var dir = platformStuffService.GetAppFolderPath();
        if (!Directory.Exists(dir)) 
            Directory.CreateDirectory(dir);

        File.WriteAllText(DbFullPath, json);
    }
}