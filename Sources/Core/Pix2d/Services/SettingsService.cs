using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Pix2d.Services;

public class SettingsService : ISettingsService {
    public const string DbName = "pix2d_settings.json";
    private readonly string _dbFullPath;

    public Dictionary<string, object> Settings { get; set; }

    private JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
    };

    public SettingsService(string dbFolderPath) {
        if (string.IsNullOrEmpty(dbFolderPath))
            dbFolderPath = Pix2DApp.AppFolder;
        _dbFullPath = System.IO.Path.Combine(dbFolderPath, DbName);

        LoadJson();
    }

    private void LoadJson()
    {
        if (!File.Exists(_dbFullPath))
        {
            Settings = new Dictionary<string, object>();
            SaveJson();
            return;
        }

        var json = File.ReadAllText(_dbFullPath);
        Settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, _serializerSettings) ?? new Dictionary<string, object>();
    }

    public T? Get<T>(string key) {
        try {
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

    public bool TryGet<T>(string key, out T? value) {
        value = default;
        try {
            value = Get<T>(key);
        }
        catch {
            return false;
        }

        return true;
    }

    public void Set<T>(string key, T value)
    {
        Settings[key] = value;
        SaveJson();
    }

    private void SaveJson()
    {
        var json = JsonConvert.SerializeObject(Settings, _serializerSettings);
        File.WriteAllText(_dbFullPath, json);
    }
}