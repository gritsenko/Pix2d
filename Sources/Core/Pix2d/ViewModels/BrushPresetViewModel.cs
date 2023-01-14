using System.Threading.Tasks;
using Mvvm;
using Pix2d.Primitives.Drawing;
using Pix2d.ViewModels.Preview;
using SkiaSharp;

namespace Pix2d.ViewModels
{
    public class BrushPresetViewModel : ViewModelBase
    {
        public BrushSettings Preset { get; }

        public SKBitmapObservable Preview { get; set; } = new SKBitmapObservable();

        public double Scale
        {
            get => Preset.Scale;
            set
            {
                Preset.Scale = value > 0 ? (float) value : 1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SizeStr));
                UpdatePreviewAsync();
                UpdateBrush();
            }
        }

        public double Opacity
        {
            get => Preset.Opacity;
            set
            {
                Preset.Opacity = (float) value;
                OnPropertyChanged();
                UpdatePreviewAsync();
                UpdateBrush();
            }
        }

        [NotifiesOn(nameof(Scale))]
        public string SizeStr => Preset.Scale + "px";

        public BrushPresetViewModel(BrushSettings preset)
        {
            Preset = preset;
            UpdatePreviewAsync();
        }

        private async void UpdatePreviewAsync()
        {
            const int size = 36;
            var src = Preset.Brush.GetPreview(Preset.Scale);
            if (src == null)
                return;

            var x = (int)(0.5f * (size - src.Width));
            var y = (int)(0.5f * (size - src.Height));

            if (Preview.Bitmap == null)
            {
                Preview.SetBitmap(new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
            }

            await Task.Run(() =>
            {
                using (var canvas = new SKCanvas(Preview.Bitmap))
                {
                    canvas.Clear();
                    canvas.DrawBitmap(src, x, y);
                }
            });

            Preview.RaiseBitmapChanged();
        }

        public void UpdateBrush()
        {
            Preset?.InitBrush();
        }

        protected bool Equals(BrushPresetViewModel other)
        {
            return Equals(Preset, other.Preset);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BrushPresetViewModel) obj);
        }

        public override int GetHashCode()
        {
            return (Preset != null ? Preset.GetHashCode() : 0);
        }
    }
}