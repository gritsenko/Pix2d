#nullable enable
using System.Net.Http;
using Newtonsoft.Json;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Infrastructure;
using Pix2d.Primitives.Palette;
using Pix2d.Services.Palette;
using SkiaSharp;

namespace Pix2d.Services;

public class PaletteService : IPaletteService
{
    private const string SavedPalettesSettingKey = "SavedPalettes"; // = nameof(AppSettings.SavedPalettes)
    private static readonly TimeSpan LospecTimeout = TimeSpan.FromSeconds(15);

    private readonly ISettingsService _settingsService;
    private readonly IFileService _fileService;
    private readonly List<SKColor> _customColors = new List<SKColor>();
    private readonly List<SKColor> _recentColors = new List<SKColor>();
    private List<PaletteData> _savedPalettes = new();

    public IReadOnlyList<SKColor> CustomPalette => _customColors;
    public IReadOnlyList<SKColor> RecentPalette => _recentColors;

    private Dictionary<string, List<SKColor>> _palettes = new Dictionary<string, List<SKColor>>();

    public event EventHandler<PaletteChangedEventArgs>? PaletteChanged;
    public event EventHandler? SavedPalettesChanged;

    public PaletteService(ISettingsService settingsService, IFileService fileService)
    {
        _settingsService = settingsService;
        _fileService = fileService;
        _palettes[nameof(RecentPalette)] = _recentColors;
        _palettes[nameof(CustomPalette)] = _customColors;
        InitDefaultColors();
        LoadSavedPalettes();
    }

    private void InitDefaultColors()
    {
        var maxItems = 5;
        for (int i = maxItems - 1; i >= 0; i--)
        {
            _customColors.Add(SKColor.FromHsv(0, 0, (float)i / maxItems * 100));
        }

        for (int i = maxItems - 1; i >= 0; i--)
        {
            _recentColors.Add(SKColor.FromHsv(0, 0, (float)i / maxItems * 100));
        }

        LoadPaletteFromSettings();
    }


    public IEnumerable<SKColor> GetPaletteColors(string paletteName)
    {
        return GetPalette(paletteName)!.ToArray();
    }

    private List<SKColor>? GetPalette(string paletteName)
    {
        if (_palettes.TryGetValue(paletteName, out var palette))
        {
            return palette;
        }

        throw new Exception("No such palette: " + paletteName);
    }

    public void InsertColor(string paletteName, SKColor color, int index = -1)
    {
        if (color == SKColor.Empty)
            return;

        var palette = GetPalette(paletteName);

        if (palette == null)
            return;

        var oldIndex = palette.IndexOf(color);
        if (oldIndex > -1)
        {
            palette.RemoveAt(oldIndex);
        }

        if (index > -1)
            palette.Insert(index, color);
        else
            palette.Add(color);

        if (palette == _recentColors && palette.Count > 5)
        {
            palette.RemoveAt(_recentColors.Count - 1);
        }

        if (palette == _customColors)
        {
            SavePaletteToSettings(palette);
        }

        OnPaletteChanged(paletteName);
    }

    public void RemoveColor(string paletteName, SKColor color)
    {
        var palette = GetPalette(paletteName);

        if (palette == null)
            return;

        palette.Remove(color);

        if (palette == _customColors)
        {
            SavePaletteToSettings(palette);
        }

        OnPaletteChanged(paletteName);
    }

    public void SetPaletteColors(string paletteName, IEnumerable<SKColor> colors)
    {
        var palette = GetPalette(paletteName);
        if (palette == null)
            return;

        palette.Clear();
        palette.AddRange(colors.Where(x => x != SKColor.Empty));

        if (palette == _customColors)
        {
            SavePaletteToSettings(palette);
        }

        OnPaletteChanged(paletteName);
    }

    // ---- Named palette library ---------------------------------------------

    public IReadOnlyList<string> GetSavedPaletteNames()
    {
        return _savedPalettes.Select(x => x.Name).ToList();
    }

    public void SaveCurrentPaletteAs(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim();
        var data = new PaletteData
        {
            Name = name,
            Colors = _customColors.Select(x => x.ToString()).ToList()
        };

        var existingIndex = _savedPalettes.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            _savedPalettes[existingIndex] = data;
        else
            _savedPalettes.Add(data);

        PersistSavedPalettes();
        OnSavedPalettesChanged();
    }

    public void LoadSavedPalette(string name)
    {
        var data = _savedPalettes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (data == null)
            return;

        SetPaletteColors(nameof(CustomPalette), ParseHexColors(data.Colors));
    }

    public void DeleteSavedPalette(string name)
    {
        var removed = _savedPalettes.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            PersistSavedPalettes();
            OnSavedPalettesChanged();
        }
    }

    // ---- File / remote import-export ---------------------------------------

    public async Task<bool> ImportPaletteFromFileAsync()
    {
        try
        {
            var files = await _fileService.OpenFileWithDialogAsync(PaletteFormats.ImportExtensions, false, "palette");
            var file = files?.FirstOrDefault();
            if (file == null)
                return false;

            byte[] bytes;
            await using (var stream = await file.OpenRead())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (!PaletteFormats.TryParse(file.Extension, bytes, out var colors, out var name))
                return false;

            if (string.IsNullOrWhiteSpace(name))
                name = GetFileTitle(file);

            ApplyImportedPalette(name!, colors);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return false;
        }
    }

    public async Task<bool> ExportPaletteToFileAsync(string suggestedName)
    {
        try
        {
            var suggested = string.IsNullOrWhiteSpace(suggestedName) ? "palette" : suggestedName;
            var result = await _fileService.GetFileToSaveWithDialogAsync(PaletteFormats.ExportExtensions, "palette", suggested);

            return await result.MatchAsync(async file =>
            {
                var colors = _customColors.ToList();
                var extension = (file.Extension ?? ".gpl").ToLowerInvariant();

                if (extension == ".png")
                {
                    await using var pngStream = new MemoryStream(PaletteFormats.WritePng(colors));
                    await file.SaveAsync(pngStream);
                }
                else if (extension is ".hex" or ".txt")
                {
                    await file.SaveAsync(PaletteFormats.WriteHexList(colors));
                }
                else
                {
                    await file.SaveAsync(PaletteFormats.WriteGpl(colors, suggested));
                }

                return true;
            }, _ => Task.FromResult(false));
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return false;
        }
    }

    public async Task<bool> ImportPaletteFromLospecAsync(string slugOrUrl)
    {
        if (string.IsNullOrWhiteSpace(slugOrUrl))
            return false;

        try
        {
            var slug = NormalizeLospecSlug(slugOrUrl);
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            using var http = new HttpClient { Timeout = LospecTimeout };
            var json = await http.GetStringAsync($"https://lospec.com/palette-list/{slug}.json");

            var colors = PaletteFormats.ParseLospecJson(json, out var name);
            if (colors.Count == 0)
                return false;

            ApplyImportedPalette(string.IsNullOrWhiteSpace(name) ? slug : name!, colors);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return false;
        }
    }

    private void ApplyImportedPalette(string name, List<SKColor> colors)
    {
        // Make the imported palette the live custom palette AND keep it in the library so it's reloadable.
        SetPaletteColors(nameof(CustomPalette), colors);
        SaveCurrentPaletteAs(name);
    }

    private static string NormalizeLospecSlug(string input)
    {
        input = input.Trim();

        if (input.Contains("lospec.com", StringComparison.OrdinalIgnoreCase))
        {
            var idx = input.IndexOf("palette-list/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                input = input[(idx + "palette-list/".Length)..];
        }

        input = input.TrimEnd('/');
        var lastSlash = input.LastIndexOf('/');
        if (lastSlash >= 0)
            input = input[(lastSlash + 1)..];

        input = input.Replace(".json", "").Replace(".gpl", "").Replace(".hex", "").Replace(".png", "");
        return input.ToLowerInvariant().Replace(' ', '-');
    }

    private static string GetFileTitle(IFileContentSource file)
    {
        var title = file.Title;
        if (string.IsNullOrWhiteSpace(title))
            title = file.Path;

        var name = System.IO.Path.GetFileNameWithoutExtension(title);
        return string.IsNullOrWhiteSpace(name) ? "Palette" : name;
    }

    private static IEnumerable<SKColor> ParseHexColors(IEnumerable<string> hexColors)
    {
        foreach (var hex in hexColors)
        {
            if (SKColor.TryParse(hex, out var color))
                yield return color;
        }
    }

    private void SavePaletteToSettings(List<SKColor> palette)
    {
        var palstr = string.Join(";", palette.Select(x => x.ToString()));

        _settingsService.Set(nameof(CustomPalette), palstr);
    }

    private void LoadPaletteFromSettings()
    {
        try
        {
            var palstr = _settingsService.Get<string>(nameof(CustomPalette));

            if (palstr != null && !string.IsNullOrWhiteSpace(palstr))
            {
                if (palstr.StartsWith("#"))
                {
                    LoadColorsFromHex(palstr, _customColors);
                }
            }

            OnPaletteChanged(nameof(CustomPalette));
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private void LoadSavedPalettes()
    {
        try
        {
            if (_settingsService.TryGet<List<PaletteData>>(SavedPalettesSettingKey, out var list) && list != null)
                _savedPalettes = list;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private void PersistSavedPalettes()
    {
        try
        {
            _settingsService.Set(SavedPalettesSettingKey, _savedPalettes);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    struct Col
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public SKColor ToSKColor()
        {
            return new SKColor(R, G, B, A);
        }
    }

    private void LoadColorsFromARGB(string palstr, List<SKColor> customColors)
    {
        var colors = JsonConvert.DeserializeObject<Col[]>(palstr);
        if (colors != null && colors.Length > 0)
        {
            _customColors.Clear();
            _customColors.AddRange(colors.Select(x => x.ToSKColor()).Where(x => x != SKColor.Empty));
        }
    }

    private void LoadColorsFromHex(string palstr, List<SKColor> customColors)
    {
        if (string.IsNullOrWhiteSpace(palstr)) return;

        var colors = palstr.Split(';');
        if (colors.Length <= 0) return;

        var palette = colors.Select(x => SKColor.Parse(x));

        customColors.Clear();
        customColors.AddRange(palette.Where(x => x != SKColor.Empty));
    }

    private void OnPaletteChanged(string paletteName)
    {
        PaletteChanged?.Invoke(this, new PaletteChangedEventArgs(paletteName));
    }

    private void OnSavedPalettesChanged()
    {
        SavedPalettesChanged?.Invoke(this, EventArgs.Empty);
    }
}
