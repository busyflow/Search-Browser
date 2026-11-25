using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace SearchBrowser
{
    // Exact 1:1 color tokens from oriel-ref/src/shared/tokens.css & driceroland/Search Design.swift
    public static class Palette
    {
        public static bool IsDark { get; private set; }

        // Solid tokens (tokens.css:8-20, 152-164)
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

        // Translucent .on-frame tokens (tokens.css:185-204)
        public static SolidColorBrush FrameWash { get; private set; }
        public static SolidColorBrush FrameHover { get; private set; }
        public static SolidColorBrush FrameTile { get; private set; }
        public static SolidColorBrush FramePinLive { get; private set; }
        public static SolidColorBrush FrameMuted { get; private set; }
        public static SolidColorBrush FrameFaint { get; private set; }

        // Ink alpha & UI surface tokens (tokens.css:34-46, menus.css:5-25, app.css:94-109)
        public static SolidColorBrush InkHoverText { get; private set; }
        public static SolidColorBrush InkMarkBg { get; private set; }
        public static SolidColorBrush InkCloseDisc { get; private set; }
        public static SolidColorBrush InkGhostEdge { get; private set; }
        public static SolidColorBrush InkPickedOutline { get; private set; }
        public static SolidColorBrush CardEdge { get; private set; }
        public static SolidColorBrush MenuBg { get; private set; }
        public static SolidColorBrush MenuBorder { get; private set; }
        public static SolidColorBrush MenuHot { get; private set; }
        public static SolidColorBrush MenuSep { get; private set; }
        public static SolidColorBrush MenuSecondary { get; private set; }
        public static SolidColorBrush CloseHoverBg { get; private set; }
        public static SolidColorBrush CloseHoverFg { get; private set; }
        public static SolidColorBrush UnreadRed { get; private set; }

        public static void ResolveTheme()
        {
            string mode = Store.Prefs != null ? Store.Prefs.Appearance : "System";
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase))
                IsDark = true;
            else if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
                IsDark = false;
            else
                IsDark = IsWindowsInDarkMode();

            UnreadRed = Freeze(Color.FromRgb(237, 61, 54)); // #ED3D36

            if (IsDark)
            {
                // Exact dark tokens (tokens.css:152-204)
                Ground = Freeze(Color.FromRgb(28, 28, 28));          // #1C1C1C
                Frame = Freeze(Color.FromRgb(19, 19, 19));           // #131313
                Ink = Freeze(Color.FromRgb(237, 237, 237));          // #EDEDED
                Muted = Freeze(Color.FromRgb(148, 148, 148));        // #949494
                Faint = Freeze(Color.FromRgb(82, 82, 82));           // #525252
                Hairline = Freeze(Color.FromRgb(51, 51, 51));        // #333333
                Wash = Freeze(Color.FromRgb(45, 45, 45));            // #2D2D2D
                Hover = Freeze(Color.FromRgb(38, 38, 38));           // #262626
                Safe = Freeze(Color.FromRgb(74, 222, 128));          // #4ADE80
                Unsafe = Freeze(Color.FromRgb(250, 191, 36));        // #FABF24
                CardSurface = Ground;                                // #1C1C1C
                OverlayScrim = Freeze(Color.FromArgb(189, 28, 28, 28)); // 74%

                FrameWash = Freeze(Color.FromArgb(24, 255, 255, 255));    // .094
                FrameHover = Freeze(Color.FromArgb(18, 255, 255, 255));   // .070
                FrameTile = Freeze(Color.FromArgb(18, 255, 255, 255));    // .070 (clear pin tile contrast)
                FramePinLive = Freeze(Color.FromArgb(38, 255, 255, 255)); // .149
                FrameMuted = Freeze(Color.FromArgb(148, 255, 255, 255));  // .580
                FrameFaint = Freeze(Color.FromArgb(74, 255, 255, 255));   // .290

                InkHoverText = Freeze(Color.FromArgb(190, 237, 237, 237));    // .75
                InkMarkBg = Freeze(Color.FromArgb(18, 237, 237, 237));        // .07
                InkCloseDisc = Freeze(Color.FromArgb(22, 237, 237, 237));     // .086
                InkGhostEdge = Freeze(Color.FromArgb(78, 237, 237, 237));     // .30 (clear drop ghost outline)
                InkPickedOutline = Freeze(Color.FromArgb(95, 237, 237, 237)); // .37
                CardEdge = Freeze(Color.FromArgb(20, 255, 255, 255));         // .08

                MenuBg = Freeze(Color.FromArgb(247, 40, 40, 40));             // rgba(40,40,40,.97)
                MenuBorder = Freeze(Color.FromArgb(46, 255, 255, 255));       // .18
                MenuHot = Freeze(Color.FromArgb(23, 255, 255, 255));          // .09
                MenuSep = Freeze(Color.FromArgb(31, 255, 255, 255));          // .12
                MenuSecondary = Freeze(Color.FromArgb(140, 255, 255, 255));   // .55
                CloseHoverBg = Freeze(Color.FromArgb(51, 232, 72, 56));       // .20
                CloseHoverFg = Freeze(Color.FromRgb(255, 107, 90));           // #FF6B5A
            }
            else
            {
                // Exact light tokens (tokens.css:8-46, 185-194)
                Ground = Freeze(Color.FromRgb(255, 255, 255));       // #FFFFFF
                Frame = Freeze(Color.FromRgb(244, 244, 244));        // #F4F4F4
                Ink = Freeze(Color.FromRgb(23, 23, 23));             // #171717
                Muted = Freeze(Color.FromRgb(140, 140, 140));        // #8C8C8C
                Faint = Freeze(Color.FromRgb(212, 212, 212));        // #D4D4D4
                Hairline = Freeze(Color.FromRgb(232, 232, 232));     // #E8E8E8
                Wash = Freeze(Color.FromRgb(239, 239, 239));         // #EFEFEF
                Hover = Freeze(Color.FromRgb(246, 246, 246));        // #F6F6F6
                Safe = Freeze(Color.FromRgb(20, 128, 61));           // #14803D
                Unsafe = Freeze(Color.FromRgb(181, 84, 10));         // #B5540A
                CardSurface = Ground;                                // #FFFFFF
                OverlayScrim = Freeze(Color.FromArgb(189, 255, 255, 255)); // 74%

                FrameWash = Freeze(Color.FromArgb(18, 0, 0, 0));     // .07
                FrameHover = Freeze(Color.FromArgb(13, 0, 0, 0));    // .05
                FrameTile = Freeze(Color.FromArgb(13, 0, 0, 0));     // .05
                FramePinLive = Freeze(Color.FromArgb(28, 0, 0, 0));  // .11
                FrameMuted = Freeze(Color.FromArgb(108, 0, 0, 0));   // .4241
                FrameFaint = Freeze(Color.FromArgb(33, 0, 0, 0));    // .1309

                InkHoverText = Freeze(Color.FromArgb(179, 23, 23, 23));    // .70
                InkMarkBg = Freeze(Color.FromArgb(15, 23, 23, 23));        // .06
                InkCloseDisc = Freeze(Color.FromArgb(18, 23, 23, 23));     // .07
                InkGhostEdge = Freeze(Color.FromArgb(41, 23, 23, 23));     // .16
                InkPickedOutline = Freeze(Color.FromArgb(89, 23, 23, 23)); // .35
                CardEdge = Freeze(Color.FromArgb(20, 0, 0, 0));            // .08

                MenuBg = Freeze(Color.FromArgb(247, 246, 246, 246));       // rgba(246,246,246,.97)
                MenuBorder = Freeze(Color.FromArgb(66, 0, 0, 0));          // .26
                MenuHot = Freeze(Color.FromArgb(16, 0, 0, 0));             // .063
                MenuSep = Freeze(Color.FromArgb(26, 0, 0, 0));             // .10
                MenuSecondary = Freeze(Color.FromArgb(128, 0, 0, 0));      // .50
                CloseHoverBg = Freeze(Color.FromArgb(31, 196, 43, 28));    // .12
                CloseHoverFg = Freeze(Color.FromRgb(196, 43, 28));         // #C42B1C
            }
        }

        public static bool IsWindowsInDarkMode()
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

        private static SolidColorBrush Freeze(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
