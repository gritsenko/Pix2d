using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Pix2d.Abstract.Platform;
using System.Globalization;

namespace Pix2d.Common;

public class LocalizationService : ILocalizationService
{
    static List<LocalizationDictionary> _strings = new();
    static LocalizationDictionary _currentStrings;
    private readonly AppState _appState;
    private readonly ISettingsService _settingsService;

    public string this[string name]
    {
        get
        {
            if (!_currentStrings.Strings.TryGetValue(name, out var value))
            {
                foreach (var dict in _strings)
                    dict.Strings.TryAdd(name, name);
                value = name;
            }
            return value;
        }
    }

    public string this[string name, params object[] arguments] => name;

    public static async Task ExportStrings(IFileService fileService)
    {
        var stringsJson = JsonConvert.SerializeObject(_strings, Formatting.Indented);

        await fileService.SaveTextToFileWithDialogAsync(stringsJson, [".json"], defaultFileName: "strings");
    }

    public LocalizationService(AppState appState, ISettingsService settingsService, Func<string> getStringsProviderFunc = null)
    {
        _appState = appState;
        _settingsService = settingsService;

        //using in tests
        if (getStringsProviderFunc != null)
        {
            _strings = JsonConvert.DeserializeObject<List<LocalizationDictionary>>(getStringsProviderFunc());
        }
        else
        {
            using var stream = ResourceManager.GetAsset("/Assets/strings.json");
            using var sr = new StreamReader(stream);
            var strings = sr.ReadToEnd();
            _strings = JsonConvert.DeserializeObject<List<LocalizationDictionary>>(strings);
        }

        if (!_strings.Any())
        {
            var defaultStrings = new LocalizationDictionary() { Locale = "en" };
            _strings.Add(defaultStrings);
        }

        var systemCulture = CultureInfo.CurrentCulture;
        var currentLocale = _settingsService.Get<string>("locale") ?? systemCulture.TwoLetterISOLanguageName;
        _appState.Locale = currentLocale;

        _currentStrings =
            _strings.FirstOrDefault(x => x.Locale.Equals(currentLocale, StringComparison.InvariantCultureIgnoreCase))
            ?? _strings.FirstOrDefault(x => x.Locale.Equals("en", StringComparison.InvariantCultureIgnoreCase))
            ?? _strings.First(); // fallback to first available if "en" is missing
    }

    public void SetLocale(string locale)
    {
        var currentLocale = locale;

        var dict = _strings.FirstOrDefault(x => x.Locale.Equals(currentLocale, StringComparison.InvariantCultureIgnoreCase));
        if (dict != null)
        {
            _currentStrings = dict;
            _settingsService.Set("locale", currentLocale);

            _appState.Locale = currentLocale;
        }
    }
}

public class LocalizationDictionary
{
    public string Locale { get; set; }
    public Dictionary<string, string> Strings = new();
}