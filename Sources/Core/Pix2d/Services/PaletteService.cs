using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Pix2d.Abstract;
using Pix2d.Primitives.Palette;
using SkiaSharp;

namespace Pix2d.Services
{
    public class PaletteService : IPaletteService
    {
        private readonly List<SKColor> _customColors = new List<SKColor>();
        private readonly List<SKColor> _recentColors = new List<SKColor>();

        public IReadOnlyList<SKColor> CustomPalette => _customColors;
        public IReadOnlyList<SKColor> RecentPalette => _recentColors;

        private Dictionary<string, List<SKColor>> _palettes = new Dictionary<string, List<SKColor>>();

        public event EventHandler<PaletteChangedEventArgs> PaletteChanged;

        public PaletteService()
        {
            _palettes[nameof(RecentPalette)] = _recentColors;
            _palettes[nameof(CustomPalette)] = _customColors;
            InitDefaultColors();
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
            return GetPalette(paletteName).ToArray();
        }

        private List<SKColor> GetPalette(string paletteName)
        {
            if (_palettes.TryGetValue(paletteName, out var palette))
            {
                return palette;
            }
            throw new Exception("No such palette: " + paletteName);
        }

        public void InsertColor(string paletteName, SKColor color, int index = -1)
        {
            if(color == SKColor.Empty)
                return;
            
            var palette = GetPalette(paletteName);

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

            palette.Remove(color);

            if (palette == _customColors)
            {
                SavePaletteToSettings(palette);
            }

            OnPaletteChanged(paletteName);
        }

        private void SavePaletteToSettings(List<SKColor> palette)
        {
            var settingsService = CommonServiceLocator.ServiceLocator.Current.GetInstance<ISettingsService>();

            var palstr = string.Join(";", palette.Select(x => x.ToString()));

            settingsService.Set(nameof(CustomPalette), palstr);
        }

        private void LoadPaletteFromSettings()
        {
            try
            {
                var settingsService = CommonServiceLocator.ServiceLocator.Current.GetInstance<ISettingsService>();
                var palstr = settingsService.Get<string>(nameof(CustomPalette));

                if (!string.IsNullOrWhiteSpace(palstr))
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

        struct Col
        {
            public byte A;
            public byte R;
            public byte G;
            public byte B;

            public SKColor ToSKColor()
            {
                return new SKColor(R,G,B,A);
            }
        }

        private void LoadColorsFromARGB(string palstr, List<SKColor> customColors)
        {
            var colors = JsonConvert.DeserializeObject<Col[]>(palstr);
            if (colors.Length > 0)
            {
                _customColors.Clear();
                _customColors.AddRange(colors.Select(x=>x.ToSKColor()).Where(x => x != SKColor.Empty));
            }
        }

        private void LoadColorsFromHex(string palstr, List<SKColor> customColors)
        {
            if (string.IsNullOrWhiteSpace(palstr)) return;

            var colors = palstr.Split(';');
            if (colors.Length <= 0) return;

            var palette = colors.Select(SKColor.Parse);

            customColors.Clear();
            customColors.AddRange(palette.Where(x => x != SKColor.Empty));
        }

        protected virtual void OnPaletteChanged(string paletteName)
        {
            PaletteChanged?.Invoke(this, new PaletteChangedEventArgs(paletteName));
        }
    }
}