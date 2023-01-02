using System;
using System.IO;
using System.Linq;
using JsonFlatFileDataStore;
using Pix2d.Abstract;
using Newtonsoft.Json;

namespace Pix2d.Services;

public class Pix2dSetting
{
    public string Key { get; set; }
    public string Data { get; set; }
}

public class SettingsService : ISettingsService
{
    public const string DbName = "pix2d_settings.json";
    private readonly string _dbFullPath;

    public SettingsService(string dbFolderPath)
    {
        if (string.IsNullOrEmpty(dbFolderPath))
            dbFolderPath = Pix2DApp.AppFolder;
        _dbFullPath = System.IO.Path.Combine(dbFolderPath, DbName);
    }

    public string GetRaw(string key)
    {
        try
        {

            using (var db = GetDb())
            {
                var col = db.GetCollection<Pix2dSetting>("settings");
                var record = col.AsQueryable().FirstOrDefault(x => x.Key == key);

                if (record != null)
                {
                    return record.Data;
                }

            }
        }
        catch (Exception ex)
        {
            DeleteOldDbFile();
            Logger.LogException(ex);
        }

        return default;
    }

    public T Get<T>(string key)
    {
        try
        {

            using (var db = GetDb())
            {
                var col = db.GetCollection<Pix2dSetting>("settings");
                var record = col.AsQueryable().FirstOrDefault(x => x.Key == key);

                if (record != null)
                {
                    return JsonConvert.DeserializeObject<T>(record.Data);
                }

            }
        }
        catch (Exception ex)
        {
            DeleteOldDbFile();
            Logger.LogException(ex);
        }

        return default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default;
        try
        {
            value = Get<T>(key);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public void Set<T>(string key, T value)
    {
        using (var db = GetDb())
        {
            var col = db.GetCollection<Pix2dSetting>("settings");

            var setting = new Pix2dSetting()
            {
                Key = key,
                Data = JsonConvert.SerializeObject(value)
            };

            var old = col.Find(x => x.Key == key).FirstOrDefault();
            if (old != null)
            {
                col.DeleteMany(x => x.Key == old.Key);
            }

            col.InsertOne(setting);

        }
    }

    private DataStore GetDb()
    {
        return new DataStore(_dbFullPath);
    }

    private void DeleteOldDbFile()
    {
        try
        {
            File.Delete(_dbFullPath);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }
}