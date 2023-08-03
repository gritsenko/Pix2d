using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pix2d.Common.Converters;
using SkiaSharp;

namespace Pix2d.Resources;

public static class StaticResources
{
    public static class Colors
    {
        public static Color PanelsBackgroundColor { get; } = "#4a525c".ToColor();
        public static Color MainBackgroundColor { get; } = "#2d2d2f".ToColor();
        public static Color ModalOverlayColor { get; } = "#882d2d2f".ToColor();
        public static Color TitleBarBackgroundColor { get; } = "#1d1d1f".ToColor();
        public static Color ForegroundColor { get; } = "#ffffff".ToColor();
        public static Color SelectedItemColor { get; } = "#FF363d45".ToColor();
        public static Color MyAccentColor { get; } = "#ff4384de".ToColor();
        public static Color ButtonHoverColor { get; } = "#AA4384de".ToColor();
        public static Color ButtonActiveColor { get; } = "#5883bf".ToColor();
        public static Color MyLinkHighlightColor { get; } = "#ff6ba7f9".ToColor();
        public static Color BrushButtonColor { get; } = "#414953".ToColor();
        public static Color InnerPanelBackgroundColor { get; } = "#3a3f46".ToColor();
    }

    public static class Brushes
    {
        public static Brush ForegroundBrush { get; } = Colors.ForegroundColor.ToBrush();
        public static Brush PanelsBackgroundBrush { get; } = Colors.PanelsBackgroundColor.ToBrush();
        public static Brush MainBackgroundBrush { get; } = Colors.MainBackgroundColor.ToBrush();
        public static Brush SelectedItemBrush { get; } = Colors.SelectedItemColor.ToBrush();
        public static Brush SelectedHighlighterBrush { get; } = Colors.MyAccentColor.ToBrush();
        public static Brush AccentBrush { get; } = Colors.MyAccentColor.ToBrush();
        public static Brush ButtonHoverBrush { get; } = Colors.ButtonHoverColor.ToBrush();
        public static Brush ButtonActiveBrush { get; } = Colors.ButtonActiveColor.ToBrush();
        public static Brush ButtonSolidBrush { get; set; } = Colors.MainBackgroundColor.ToBrush();
        public static Brush LinkHighlightBrush { get; } = Colors.MyLinkHighlightColor.ToBrush();
        public static Brush CheckerTilesBrush { get; } = new ImageBrush(StaticResources.CheckerTilesBitmap);
        public static Brush CheckerTilesBrushNoScale { get; } = new ImageBrush(StaticResources.CheckerTilesBitmap) { Stretch = Stretch.None };
        public static Brush ActionsBarBackground { get; set; } = "#444E59".ToColor().ToBrush();
        public static Brush ModalOverlayBrush { get; set; } = Colors.ModalOverlayColor.ToBrush();
        public static Brush SecondaryButtonBrush { get; set; } = Colors.SelectedItemColor.ToBrush();
        public static Brush BrushButtonBrush { get; set; } = Colors.BrushButtonColor.ToBrush();
        public static Brush InnerPanelBackgroundBrush { get; } = Colors.InnerPanelBackgroundColor.ToBrush();
    }

    public static class Fonts
    {
        public static FontFamily IconFontSegoe { get; } =
            new FontFamily(GetEmbeddedResourceURI("/Assets/Fonts/"), "segmdl2.ttf#Segoe MDL2 Assets");

        public static FontFamily Pix2dThemeFontFamily { get; } =
            new FontFamily(GetEmbeddedResourceURI("/Assets/Fonts/"), "pix2d.ttf#pix2d");

        public static FontFamily IconsThemeFontFamily { get; } =
            new FontFamily(GetEmbeddedResourceURI("/Assets/Fonts/"), "icons.ttf#icons");
        //public static FontFamily FluentIcons { get; } = 
        //    new FontFamily(new Uri("avares://Pix2d.Core/Assets/Fonts/"), "Segoe Fluent Icons.ttf#Segoe Fluent Icons");
    }

    public static class Converters
    {
        public static ColorToSKColorConverter ColorToSkColorConverter { get; } = new();
        public static FuncValueConverter<SKColor, IBrush> SKColorToBrushConverter { get; } = new(v => v.ToBrush());
        //public static SKBitmapToBrushConverter SKBitmapToBrushConverter { get; } = new();

        public static FuncValueConverter<SKBitmap, IBrush> SKBitmapToIBrushConverter = new FuncValueConverter<SKBitmap, IBrush>(v => v != null ? new ImageBrush(v.ToBitmap()) : Avalonia.Media.Brushes.Transparent);
        public static IValueConverter InverseBooleanConverter { get; } = new FuncValueConverter<bool, bool>(b => !b);

        public static FuncValueConverter<bool, IBrush> BoolToBrushButtonForegroundConverter = new(v => v ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Gray);

        public static FuncValueConverter<bool, IBrush> BoolToBrushItemBackgroundConverter = new(v => v ? StaticResources.Brushes.SelectedHighlighterBrush : Avalonia.Media.Brushes.Transparent);

    }

    public static class Templates
    {
        public static IDataTemplate ToolIconTemplateSelector { get; } = ToolIcons.ToolIconTemplateSelector;

        public static FuncTemplate<Panel> WrapPanelTemplate { get; } = new(() => new WrapPanel());
    }

    public static class Icons
    {
        public static Geometry GridIcon = Geometry.Parse("M 2.5 1 C 1.675781 1 1 1.675781 1 2.5 L 1 12.5 C 1 13.324219 1.675781 14 2.5 14 L 12.5 14 C 13.324219 14 14 13.324219 14 12.5 L 14 2.5 C 14 1.675781 13.324219 1 12.5 1 Z M 2.5 2 L 5 2 L 5 5 L 2 5 L 2 2.5 C 2 2.21875 2.21875 2 2.5 2 Z M 6 2 L 9 2 L 9 5 L 6 5 Z M 10 2 L 12.5 2 C 12.78125 2 13 2.21875 13 2.5 L 13 5 L 10 5 Z M 2 6 L 5 6 L 5 9 L 2 9 Z M 6 6 L 9 6 L 9 9 L 6 9 Z M 10 6 L 13 6 L 13 9 L 10 9 Z M 2 10 L 5 10 L 5 13 L 2.5 13 C 2.21875 13 2 12.78125 2 12.5 Z M 6 10 L 9 10 L 9 13 L 6 13 Z M 10 10 L 13 10 L 13 12.5 C 13 12.78125 12.78125 13 12.5 13 L 10 13 Z ");
    }

    public static Bitmap AppIcon { get; set; } =
        new Bitmap(GetAsset(GetEmbeddedResourceURI("/Assets/app1.png")));

    public static Bitmap ColorThumb { get; set; } =
        new Bitmap(GetAsset(GetEmbeddedResourceURI("/Assets/ColorThumb.png")));

    public static Bitmap CheckerTilesBitmap { get; set; } =
        new(GetAsset(GetEmbeddedResourceURI("/Assets/BackgroundTile100.png")));

    private static Stream GetAsset(Uri uri) => AssetLoader.Open(uri);
    private static Uri GetEmbeddedResourceURI(string path) => new($"avares://Pix2d.Core/{path.TrimStart('/')}");
}