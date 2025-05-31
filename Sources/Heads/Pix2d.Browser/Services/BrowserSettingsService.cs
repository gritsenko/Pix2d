using System.Runtime.InteropServices.JavaScript;
using Newtonsoft.Json;
using Pix2d.Abstract.Services;

namespace Pix2d.Browser.Services;

internal static partial class MyLocalStorage
{
    [JSImport("get", "main.js")]
    internal static partial string? GetItem(string key);

    [JSImport("set", "main.js")]
    internal static partial void SetItem(string key, string value);

    [JSImport("remove", "main.js")]
    internal static partial void RemoveItem(string key);

    [JSImport("clear", "main.js")]
    internal static partial void Clear();
}

public class BrowserSettingsService : ISettingsService
{
    public T? Get<T>(string key)
    {
        var strValue = MyLocalStorage.GetItem(key);

        if (strValue == null)
            return default;

        return JsonConvert.DeserializeObject<T>(strValue);
    }

    public void Set<T>(string key, T? value)
    {
        if (value == null)
            return;

        var json = JsonConvert.SerializeObject(value);
        MyLocalStorage.SetItem(key, json);
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
}