using Avalonia.Markup.Declarative;
using Avalonia.Media.Imaging;
using Pix2d.Common.Converters;
using Pix2d.Common.Extensions;
using Pix2d.Resources;
using SkiaSharp;

namespace Pix2d.UI.Resources;

public static class StaticResources
{
    public static class Colors
    {
        public static Color ForegroundColor { get; } = "#ffffff".ToColor().WithAlpha(0.7f);
        public static Color SceneBackgroundColor { get; set; } = "#1C1C1E".ToColor();
        public static Color PanelsBorderColor { get; } = "#2C2C2E".ToColor();
        public static Color PanelsBackgroundColor { get; } = "#060608".ToColor().WithAlpha(0.95f);
        public static Color MainBackgroundColor { get; } = "#2d2d2f".ToColor();
        public static Color MainMenuBackgroundColor { get; } = "#1C1C1E".ToColor();
        public static Color ModalOverlayColor { get; } = "#882d2d2f".ToColor();
        public static Color TitleBarBackgroundColor { get; } = "#1d1d1f".ToColor();
        public static Color SelectedItemColor { get; } = "#FFFFFF".ToColor().WithAlpha(0.2f);
        public static Color MyAccentColor { get; } = "#ff4384de".ToColor();
        public static Color ButtonBackgroundColor { get; } = "#060608".ToColor().WithAlpha(0.8f);
        public static Color ButtonHoverColor { get; } = "#ffffff".ToColor().WithAlpha(0.3f);
        public static Color ButtonActiveColor { get; } = "#5883bf".ToColor();
        public static Color MyLinkHighlightColor { get; } = "#ff6ba7f9".ToColor();
        public static Color BrushButtonColor { get; } = "#FFFFFF".ToColor().WithAlpha(0.2f);
        public static Color BrushItemColor { get; } = "#FFFFFF".ToColor().WithAlpha(0.1f);
        public static Color InnerPanelBackgroundColor { get; } = "#3a3f46".ToColor();

    }

    public static class Brushes
    {
        /*
         *border-radius: var(--radius-toolbar-item, 12px);
           border: 1px solid var(--stroke-primary, rgba(255, 255, 255, 0.30));
           background: var(--Accent, linear-gradient(135deg, #FF6B00 0%, #E5B407 100%));
         */
        public static Brush SelectedToolBrush { get; } =
            new LinearGradientBrush()
                .EndPoint(new Point(0, 1), RelativeUnit.Relative)
                .GradientStops([new GradientStop("#FF6B00".ToColor(), 0), new GradientStop("#E5B407".ToColor(), 1)]);

        public static Brush SelectedToolBorderBrush { get; } = Avalonia.Media.Colors.White.WithAlpha(0.3f).ToBrush();


        public static Brush ForegroundBrush { get; } = Colors.ForegroundColor.ToBrush();
        public static Brush PanelsBackgroundBrush { get; } = Colors.PanelsBackgroundColor.ToBrush();
        public static Brush PanelsBorderBrush { get; } = Colors.PanelsBorderColor.ToBrush();
        public static Brush MainBackgroundBrush { get; } = Colors.MainBackgroundColor.ToBrush();
        public static IImmutableBrush MainMenuBackgroundBrush { get; } = Colors.MainMenuBackgroundColor.ToBrush().ToImmutable();
        public static Brush SelectedItemBrush { get; } = Colors.SelectedItemColor.ToBrush();
        public static Brush AccentBrush { get; } = SelectedToolBrush;//Colors.MyAccentColor.ToBrush();
        public static Brush ButtonBackgroundBrush { get; } = Colors.ButtonBackgroundColor.ToBrush();
        public static Brush ButtonHoverBrush { get; } = Colors.ButtonHoverColor.ToBrush();
        public static Brush ButtonActiveBrush { get; } = Colors.ButtonActiveColor.ToBrush();
        public static Brush ButtonSolidBrush { get; set; } = Colors.MainBackgroundColor.ToBrush();
        public static Brush LinkHighlightBrush { get; } = Colors.MyLinkHighlightColor.ToBrush();
        public static Brush CheckerTilesBrush { get; } = new ImageBrush(StaticResources.CheckerTilesBitmap) {TileMode = TileMode.Tile, Stretch = Stretch.Uniform, Transform = new ScaleTransform(0.2,0.2)};
        public static Brush CheckerTilesBrushNoScale { get; } = new ImageBrush(StaticResources.CheckerTilesBitmap) { TileMode = TileMode.FlipXY, DestinationRect = new RelativeRect(0, 0, 100, 100, RelativeUnit.Absolute)};
        public static Brush ActionsBarBackground { get; set; } = "#444E59".ToColor().ToBrush();
        public static Brush ModalOverlayBrush { get; set; } = Colors.ModalOverlayColor.ToBrush();
        public static Brush SelectedToggleButtonBrush { get; set; } = Colors.BrushButtonColor.ToBrush();
        public static Brush BrushButtonBrush { get; set; } = Colors.BrushButtonColor.ToBrush();
        public static Brush BrushItemBrush { get; set; } = Colors.BrushItemColor.ToBrush();
        public static Brush InnerPanelBackgroundBrush { get; } = Colors.InnerPanelBackgroundColor.ToBrush();
    }

    public static class Fonts
    {
        public static FontFamily TextArticlesFontFamily =>
            new FontFamily("Segoe UI");

        public static FontFamily IconFontSegoe { get; } =
            new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/"), "segmdl2.ttf#Segoe MDL2 Assets");

        public static FontFamily Pix2dThemeFontFamily { get; } =
            new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/"), "pix2d.ttf#pix2d");

        public static FontFamily IconsThemeFontFamily { get; } =
            new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/"), "icons.ttf#icons");
        //public static FontFamily FluentIcons { get; } = 
        //    new FontFamily(new Uri("avares://Pix2d.Core/Assets/Fonts/"), "Segoe Fluent Icons.ttf#Segoe Fluent Icons");

        //v3 font without cyrillic
        //public static FontFamily DefaultTextFontFamily { get; } =
        //    new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/v3/"), "ProtoMono-Regular.ttf#Proto Mono");
        
        //v3_1 font with cyrillic
        public static FontFamily DefaultTextFontFamily =>
            new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/v31/"), "zed-mono-extended.ttf#Zed Mono");

        public static FontFamily Pix2dIconFontFamilyV3 { get; } =
            new FontFamily(ResourceManager.GetEmbeddedResourceURI("/Assets/Fonts/v3/"), "pix2d-icons-v3.ttf#pix2d-icons-v3");

    }

    public static class Converters
    {
        public static ColorToSKColorConverter ColorToSkColorConverter { get; } = new();
        public static FuncValueConverter<SKColor, IBrush> SKColorToBrushConverter { get; } = new(v => v.ToBrush());
        //public static SKBitmapToBrushConverter SKBitmapToBrushConverter { get; } = new();

        public static FuncValueConverter<SKBitmap, IBrush> SKBitmapToIBrushConverter = new FuncValueConverter<SKBitmap, IBrush>(v => v != null ? new ImageBrush(v.ToBitmap()) : Avalonia.Media.Brushes.Transparent);
        public static IValueConverter InverseBooleanConverter { get; } = new FuncValueConverter<bool, bool>(b => !b);

        public static FuncValueConverter<bool, IBrush> BoolToBrushButtonForegroundConverter = new(v => v ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Gray);

        public static FuncValueConverter<bool, IBrush> BoolToBrushItemBackgroundConverter = new(v => v ? StaticResources.Brushes.SelectedToolBrush : Avalonia.Media.Brushes.Transparent);

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

    public static class Measures
    {
        public static double PipkaCornerRadius = 3;
        public static double ToolItemCornerRadius = 12;
        public static double ButtonCornerRadius = 16;
        public static double PanelCornerRadius = 16;
        public static double PanelMargin = 12;
        public static double SmallButtonCornerRadius = 10;
        public static double SmallButtonSize = 36;
    }

    public static Bitmap UltimateImage { get; set; } =
        new(ResourceManager.GetAsset("/Assets/ULTIMATE.png"));

    public static Bitmap EssentialsImage { get; set; } =
        new(ResourceManager.GetAsset("/Assets/essentials.png"));

    public static Bitmap ProImage { get; set; } =
        new(ResourceManager.GetAsset("/Assets/PRO.png"));

    public static Bitmap ColorThumb { get; set; } =
        new(ResourceManager.GetAsset("/Assets/ColorThumb.png"));

    public static Bitmap CheckerTilesBitmap { get; set; } =
        new(ResourceManager.GetAsset("/Assets/checker.png"));
    
    public static SKBitmap WatermarkBitmap { get; set; } =
        SKBitmap.Decode(ResourceManager.GetAsset("/Assets/Watermark.png"));

    public static Bitmap NoPreview { get; set; } = new(ResourceManager.GetAsset("/Assets/no_preview.png"));

}