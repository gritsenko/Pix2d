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
                    return (T?)Coerce(value, typeof(T));
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
                    value = (T?)Coerce(propValue, typeof(T));
                    return value is not null;
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

    /// <summary>
    /// Convert.ChangeType only handles IConvertible (primitives, string, DateTime, ...).
    /// AppSettings can hold complex types declared as <c>object</c> — System.Text.Json
    /// restores those as <see cref="System.Text.Json.JsonElement"/>, which is NOT
    /// IConvertible and would throw "Object must implement IConvertible".
    /// We route JsonElement values through the JSON deserializer; everything else
    /// falls back to the original Convert.ChangeType behaviour.
    /// </summary>
    private object? Coerce(object value, Type targetType)
    {
        if (value is System.Text.Json.JsonElement je)
        {
            // Deserialize directly to the requested type.
            return je.Deserialize(targetType, _serializerOptions);
        }

        if (targetType.IsInstanceOfType(value))
            return value;

        return Convert.ChangeType(value, targetType);
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

    /// <summary>
    /// Keys already reported by <see cref="Set{T}"/> as unbacked, so a setting written on every
    /// launch/interaction warns once per process instead of spamming the log.
    /// </summary>
    private static readonly HashSet<string> _reportedMissingKeys = new(StringComparer.OrdinalIgnoreCase);

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
                return;
            }

            // A key with no writable AppSettings property is a silent write loss: the value never reaches
            // disk and every later Get/TryGet returns default. That silence is what let the whole rate-prompt
            // gate ("LaunchTime"/"IsAppReviewed"/… were never declared) misbehave for a full release, so make
            // it loud — the fix is always to add the property to AppSettings, never to swallow this.
            if (_reportedMissingKeys.Add(key))
                Logger.Log($"SettingsService: setting '{key}' is not backed by an AppSettings property — value was NOT persisted.");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
