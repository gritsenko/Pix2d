using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Media.Fonts;
using Avalonia.Media.TextFormatting;
using Pix2d.Resources;

namespace Pix2d.Common
{
    public class MyLabel : Control
    {
        private readonly TextLayout _tl;

        public MyLabel()
        {
            //var font = FontFamily.Parse("#Segoe MDL2 Assets", new Uri("avares://Pix2d.AvaloniaCore/Assets/Fonts/segmdl2.ttf"));
            var refFont = StaticResources.Fonts.IconFontSegoe;
            //var font = FontFamily.Parse("resm:Pix2d.AvaloniaCore.Assets.Fonts.segmdl2.ttf?assemply=Pix2d.AvaloniaCore#Segoe MDL2 Assets");
            var font = new FontFamily("avares://Pix2d.AvaloniaCore/Assets/Fonts#Segoe MDL2 Assets");
            //        var font = FontFamily.Parse("#Segoe MDL2 Assets", new Uri("avares://Pix2d.AvaloniaCore/Assets/Fonts/segmdl2.ttf"));
            var typeface = new Typeface(font, FontStyle.Normal, FontWeight.Normal);

            var tfg = FontManager.Current.GetOrAddGlyphTypeface(typeface);

            var assets = FontFamilyLoader.LoadFontAssets(font.Key);

            _tl = new TextLayout("\xE7A6", typeface, 22, new SolidColorBrush(Colors.White));

            FontManager.Current.TryMatchCharacter('\xE7A6', FontStyle.Normal, FontWeight.Normal, refFont, null, out var tf);

            //var tb = new TextBlock()
            //{
            //    FontFamily = font,
            //    Foreground = Colors.White.ToBrush(),
            //    FontSize = 33,
            //    Text = "\xE7A6"
            //};
            //g.Add(tb);

        }

        public override void Render(DrawingContext context)
        {
            //base.Render(context);
            context.DrawRectangle(new SolidColorBrush(Colors.Black), new Pen(), new Rect(0, 0, 200, 200));
            _tl.Draw(context/*, new Point(50, 50)*/);
        }

    }
}
