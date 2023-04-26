using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Drawing.Tools;
using Pix2d.Messages;
using Pix2d.Mvvm;
using SkiaSharp;

namespace Pix2d.ViewModels.Color
{
    public class ColorPickerViewModel : Pix2dViewModelBase
    {
        public AppState AppState { get; }
        public IMessenger Messenger { get; }
        public IPaletteService PaletteService { get; }
        private IDrawingService DrawingService { get; }

        public ObservableCollection<SKColor> CustomColors { get; set; } = new();
        public ObservableCollection<SKColor> RecentColors { get; set; } = new();

        public const string HexColorType = "Hex";
        public const string RgbColorType = "RGB";
        public const string HsvColorType = "HSV";

        public List<string> ColorTypes { get; } = new List<string>
        {
            HexColorType,
            RgbColorType,
            HsvColorType
        };

        private SKColor previousColor;

        private byte _rgbRPart;
        private byte _rgbGPart;
        private byte _rgbBPart;

        private float _hsvHPart;
        private float _hsvSPart;
        private float _hsvVPart;

        private string _hexValue;

        public SKColor SelectedColor
        {
            get => AppState.DrawingState.CurrentColor;
            set => DrawingService.SetCurrentColor(value);
        }

        public ColorPickerColorType ColorType
        {
            get => Get<ColorPickerColorType>();
            set
            {
                if (Set(value))
                {
                    UpdateEditors();
                }
            }
        }

        [NotifiesOn(nameof(ColorType))]
        public int ColorTypeIndex
        {
            get => (int) ColorType;
            set => ColorType = (ColorPickerColorType) value;
        }

        public bool EditorMode
        {
            get => Get<bool>();
            set
            {
                if (Set(value))
                {
                    UpdateEditors();
                }
            }
        }

        #region RGB

        public byte RedColorPart
        {
            get => _rgbRPart;
            set
            {
                if(value == _rgbRPart)
                    return;

                SelectedColor = new SKColor(value, _rgbGPart, _rgbBPart);
            }
        }

        public byte GreenColorPart
        {
            get => _rgbGPart;
            set
            {
                if(value == _rgbGPart)   
                    return;

                SelectedColor = new SKColor(_rgbRPart, value, _rgbBPart);
            }
        }

        public byte BlueColorPart
        {
            get => _rgbBPart;
            set
            {
                if(value == _rgbBPart)
                    return;

                SelectedColor = new SKColor(_rgbRPart, _rgbGPart, value);
            }
        }

        #endregion

        #region HSV

        public float HsvHPart
        {
            get => _hsvHPart;
            set
            {
                if(Math.Abs(value - _hsvHPart) < float.Epsilon)
                    return;

                SelectedColor = SKColor.FromHsv(value, _hsvSPart, _hsvVPart);
            }
        }

        public float HsvSPart
        {
            get => _hsvSPart;
            set
            {
                if(Math.Abs(value - _hsvSPart) < float.Epsilon)
                    return;

                SelectedColor = SKColor.FromHsv(_hsvHPart, value, _hsvVPart);
            }
        }

        public float HsvVPart
        {
            get => _hsvVPart;
            set
            {
                if(Math.Abs(value - _hsvVPart) < float.Epsilon)
                    return;

                SelectedColor = SKColor.FromHsv(_hsvHPart, _hsvSPart, value);
            }
        }

        #endregion

        //[NotifiesOn(nameof(SelectedColor))]
        public string HexValue
        {
            get => _hexValue;
            set => _hexValue = value;
        }

        public SKColor PreviousColor
        {
            get => Get<SKColor>();
            set
            {
                if (Set(value))
                {
                    previousColor = SelectedColor;
                }
            }
        }

        public bool IsEyedropperSelected => AppState.CurrentProject.CurrentTool is EyedropperTool;

        public ICommand OnAddColorCommand => GetCommand<SKColor>((c) =>
        {
            PaletteService.InsertColor(nameof(PaletteService.CustomPalette), c, -1);
        });

        public ICommand OnRemoveColorCommand => GetCommand<SKColor>((c) =>
        {
            if (c == default)
            {
                c = SelectedColor;
            }

            if (c != default)
            {
                PaletteService.RemoveColor(nameof(PaletteService.CustomPalette), c);
            }
        });

        public ICommand SetColorCommand => GetCommand<SKColor>(c =>
        {
            SelectedColor = c;
        });


        public ColorPickerViewModel(IDrawingService drawingService, IPaletteService paletteService,
            AppState appState, IMessenger messenger)
        {
            AppState = appState;
            Messenger = messenger;
            
            if (IsDesignMode) return;

            DrawingService = drawingService;
            PaletteService = paletteService;

            LoadColors();

            AppState.DrawingState.WatchFor(x => x.CurrentColor, OnDrawingStateColorChanged);

            DrawingService.Drawn += DrawingService_Drawn;

            Messenger.Register<CurrentToolChangedMessage>(this, OnCurrentToolChanged);

            PaletteService.PaletteChanged += PaletteService_PaletteChanged;
        }

        private void OnDrawingStateColorChanged()
        {
            if (SelectedColor.Equals(previousColor))
                return;

            UpdateEditors();
            previousColor = SelectedColor;
            OnPropertyChanged(nameof(SelectedColor));
        }

        private void UpdateEditors()
        {
            var value = SelectedColor;

            if (ColorType == ColorPickerColorType.Rgb)
            {
                _rgbRPart = value.Red;
                _rgbGPart = value.Green;
                _rgbBPart = value.Blue;
                OnPropertyChanged(nameof(RedColorPart));
                OnPropertyChanged(nameof(GreenColorPart));
                OnPropertyChanged(nameof(BlueColorPart));
            }


            if (ColorType == ColorPickerColorType.Hsv)
            {
                value.ToHsv(out var hsvHPart, out var hsvSPart, out var hsvVPart);
                _hsvHPart = (float)Math.Floor(hsvHPart);
                _hsvSPart = (float)Math.Floor(hsvSPart);
                _hsvVPart = (float)Math.Floor(hsvVPart);

                OnPropertyChanged(nameof(HsvHPart));
                OnPropertyChanged(nameof(HsvSPart));
                OnPropertyChanged(nameof(HsvVPart));
            }

            if (ColorType == ColorPickerColorType.Hex)
            {
                _hexValue = $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
                OnPropertyChanged(nameof(HexValue));
            }
        }

        private void OnCurrentToolChanged(CurrentToolChangedMessage obj)
        {
            OnPropertyChanged(nameof(IsEyedropperSelected));
        }

        private void PaletteService_PaletteChanged(object sender, Primitives.Palette.PaletteChangedEventArgs e)
        {
            LoadColors(e.PaletteName);
        }

        private void DrawingService_Drawn(object sender, System.EventArgs e)
        {
            if(RecentColors[0] == SelectedColor)
                return;

            PaletteService.InsertColor(nameof(PaletteService.RecentPalette), SelectedColor, 0);

            var recentColors = PaletteService.RecentPalette;
            RecentColors.Clear();
            foreach (var c in recentColors)
                RecentColors.Add(c);
        }

        private void LoadColors(string paletteName = default)
        {
            if (paletteName == default || paletteName == nameof(PaletteService.CustomPalette))
                LoadPalette(CustomColors, PaletteService.CustomPalette);

            if (paletteName == default || paletteName == nameof(PaletteService.RecentPalette))
                LoadPalette(RecentColors, PaletteService.RecentPalette);
            //PreviousColor = SKColor.FromHsv(0, 0, 50);
        }
        void LoadPalette(ObservableCollection<SKColor> targetColl, IEnumerable<SKColor> src)
        {
            targetColl.Clear();
            foreach (var customColor in src)
                targetColl.Add(customColor);
        }

        public void ApplyHexInput()
        {

            if (SKColor.TryParse(_hexValue, out var parsedColor))
            {
                _hexValue = $"#{parsedColor.Red:X2}{parsedColor.Green:X2}{parsedColor.Blue:X2}";
                SelectedColor = parsedColor;
                UpdateEditors();
            }
            else
            {
                CancelHexInput();
            }
        }

        public void CancelHexInput()
        {
            HexValue = SelectedColor.ToString();
            UpdateEditors();
        }
    }
}