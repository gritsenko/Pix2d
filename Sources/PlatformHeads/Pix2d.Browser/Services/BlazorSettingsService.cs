using System.IO;
using Pix2d.Abstract;

namespace Pix2d.Browser.Services;

public class BlazorSettingsService : ISettingsService
{
    public const string DbName = "pix2d_settings.json";
    private readonly string _dbFullPath;


    public BlazorSettingsService(string dbFolderPath)
    {
        if (string.IsNullOrEmpty(dbFolderPath))
            dbFolderPath = Pix2DApp.AppFolder;
        _dbFullPath = Path.Combine(dbFolderPath, DbName);
    }

    public string GetRaw(string key)
    {
        return default;
    }

    public T Get<T>(string key)
    {
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
    }
}