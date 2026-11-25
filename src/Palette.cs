using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Design.swift
    public static class Palette
    {
        public static bool IsDark { get; private set; }

        public static SolidColorBrush Ground { get; private set; }
        public static SolidColorBrush Frame { get; private set; }
        public static SolidColorBrush Ink { get; private set; }
        public static SolidColorBrush Muted { get; private set; }
        public static SolidColorBrush Faint { get; private set; }
        public static SolidColorBrush Hairline { get; private set; }
        public static SolidColorBrush Wash { get; private set; }
        public static SolidColorBrush Hover { get; private set; }
        public static SolidColorBrush Safe { get; private set; }
        public static SolidColorBrush Unsafe { get; private set; }
        public static SolidColorBrush CardSurface { get; private set; }
        public static SolidColorBrush OverlayScrim { get; private set; }

        public static void ResolveTheme()
        {
            string mode = Store.Prefs != null ? Store.Prefs.Appearance : "System";
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                IsDark = true;
            }
            else if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
            {
                IsDark = false;
            }
            else
            {
                IsDark = IsWindowsInDarkMode();
            }

            if (IsDark)
            {
                // Exact dark sRGB ratios from Design.swift Palette.NS
                Ground = BrushFromWhite(0.105);      // #1B1B1B
                Frame = BrushFromWhite(0.065);       // #101010
                Ink = BrushFromWhite(0.93);          // #EDEDED
                Muted = BrushFromWhite(0.58);        // #949494
                Faint = BrushFromWhite(0.32);        // #525252
                Hairline = BrushFromWhite(0.14);     // #242424
                Wash = BrushFromWhite(0.175);        // #2D2D2D
                Hover = BrushFromWhite(0.15);        // #262626
                Safe = BrushFromRgb(0.29, 0.87, 0.50);
                Unsafe = BrushFromRgb(0.98, 0.75, 0.14);
                CardSurface = BrushFromWhite(0.14);
                OverlayScrim = new SolidColorBrush(Color.FromArgb(190, 19, 19, 19));
            }
            else
            {
                // Exact light sRGB ratios from Design.swift Palette.NS
                Ground = BrushFromWhite(1.0);        // #FFFFFF
                Frame = BrushFromWhite(0.955);       // #F4F4F4
                Ink = BrushFromWhite(0.09);          // #171717
                Muted = BrushFromWhite(0.55);        // #8C8C8C
                Faint = BrushFromWhite(0.83);        // #D4D4D4
                Hairline = BrushFromWhite(0.91);     // #E8E8E8
                Wash = BrushFromWhite(0.937);        // #EFEFEF
                Hover = BrushFromWhite(0.965);       // #F6F6F6
                Safe = BrushFromRgb(0.08, 0.50, 0.24);
                Unsafe = BrushFromRgb(0.71, 0.33, 0.04);
                CardSurface = BrushFromWhite(0.99);
                OverlayScrim = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255));
            }

            Ground.Freeze();
            Frame.Freeze();
            Ink.Freeze();
            Muted.Freeze();
            Faint.Freeze();
            Hairline.Freeze();
            Wash.Freeze();
            Hover.Freeze();
            Safe.Freeze();
            Unsafe.Freeze();
            CardSurface.Freeze();
            OverlayScrim.Freeze();
        }

        private static bool IsWindowsInDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int) return ((int)val) == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static SolidColorBrush BrushFromWhite(double w)
        {
            byte b = (byte)Math.Max(0, Math.Min(255, Math.Round(w * 255.0)));
            return new SolidColorBrush(Color.FromRgb(b, b, b));
        }

        private static SolidColorBrush BrushFromRgb(double r, double g, double b)
        {
            return new SolidColorBrush(Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, Math.Round(r * 255.0))),
                (byte)Math.Max(0, Math.Min(255, Math.Round(g * 255.0))),
                (byte)Math.Max(0, Math.Min(255, Math.Round(b * 255.0)))));
        }
    }
}
