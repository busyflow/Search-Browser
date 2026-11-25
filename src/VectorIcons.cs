using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SearchBrowser
{
    // Exact vector paths from oriel-ref/src/ui/sidebar/glyphs.tsx, ui/chrome/Buttons.tsx, and ui/lib/icons.tsx
    public static class VectorIcons
    {
        // Sidebar & Helm glyphs (glyphs.tsx:52-97, 16x16 viewBox)
        public const string BackPath = "M 10.2,2.6 L 4.8,8 L 10.2,13.4";
        public const string ForwardPath = "M 5.8,2.6 L 11.2,8 L 5.8,13.4";
        public const string ReloadPath = "M 13.2,8.4 A 5.2,5.2 0 1 1 11.5,4 M 12.2,1.6 L 12.2,4.7 L 9.1,4.7";
        public const string StopPath = "M 4.2,4.2 L 11.8,11.8 M 11.8,4.2 L 4.2,11.8";
        public const string PlusPath = "M 8,2.2 L 8,13.8 M 2.2,8 L 13.8,8";
        public const string CloseSmallPath = "M 4.4,4.4 L 11.6,11.6 M 11.6,4.4 L 4.4,11.6";
        public const string MinusSmallPath = "M 4,8 L 12,8";
        public const string SearchPath = "M 11.2,7 A 4.2,4.2 0 1 1 2.8,7 A 4.2,4.2 0 1 1 11.2,7 M 10.2,10.2 L 13.5,13.5";
        public const string SidebarPath = "M 4.5,3 L 11.5,3 C 12.6,3 13.5,3.9 13.5,5 L 13.5,11 C 13.5,12.1 12.6,13 11.5,13 L 4.5,13 C 3.4,13 2.5,12.1 2.5,11 L 2.5,5 C 2.5,3.9 3.4,3 4.5,3 Z M 6.3,3 L 6.3,13";
        public const string SplitPath = "M 4.5,3 L 11.5,3 C 12.6,3 13.5,3.9 13.5,5 L 13.5,11 C 13.5,12.1 12.6,13 11.5,13 L 4.5,13 C 3.4,13 2.5,12.1 2.5,11 L 2.5,5 C 2.5,3.9 3.4,3 4.5,3 Z M 8,3 L 8,13";
        public const string PuzzlePath = "M 6,2.8 A 1.4,1.4 0 0 1 8.8,2.8 L 8.8,3.8 L 11.4,3.8 L 11.4,6.4 L 12.4,6.4 A 1.4,1.4 0 0 1 12.4,9.2 L 11.4,9.2 L 11.4,12.4 L 8.6,12.4 L 8.6,11.4 A 1.4,1.4 0 0 0 5.8,11.4 L 5.8,12.4 L 2.6,12.4 L 2.6,9.2 L 3.6,9.2 A 1.4,1.4 0 0 0 3.6,6.4 L 2.6,6.4 L 2.6,3.8 L 6,3.8 Z";
        public const string ExtensionPath = PuzzlePath;
        public const string FolderPath = "M 2.5,5.2 C 2.5,4.3 3.2,3.6 4.1,3.6 L 6.8,3.6 L 8.2,5.1 L 11.9,5.1 C 12.8,5.1 13.5,5.8 13.5,6.7 L 13.5,11.2 C 13.5,12.1 12.8,12.8 11.9,12.8 L 4.1,12.8 C 3.2,12.8 2.5,12.1 2.5,11.2 Z";
        public const string BookmarkPath = "M 4.6,3.4 C 4.6,2.74 5.14,2.2 5.8,2.2 L 10.2,2.2 C 10.86,2.2 11.4,2.74 11.4,3.4 L 11.4,13.6 L 8,11.1 L 4.6,13.6 Z";
        public const string HomePath = "M 2.2,7.8 L 8,2.6 L 13.8,7.8 M 4,6.4 L 4,13.4 L 12,13.4 L 12,6.4 M 6.6,13.4 L 6.6,9.8 L 9.4,9.8 L 9.4,13.4";
        public const string EyeOffPath = "M 2.2,8 C 3.8,5 5.8,3.8 8,3.8 C 10.2,3.8 12.2,5 13.8,8 C 12.2,11 10.2,12.2 8,12.2 C 5.8,12.2 3.8,11 2.2,8 Z M 3,3 L 13,13";
        public const string DownloadsPath = "M 8,2.8 L 8,10.2 M 5,7.4 L 8,10.5 L 11,7.4 M 3,13 L 13,13";
        public const string ChevronUpPath = "M 3.6,10.2 L 8,5.8 L 12.4,10.2";
        public const string ChevronDownPath = "M 3.6,5.8 L 8,10.2 L 12.4,5.8";
        public const string CheckPath = "M 3.2,8.4 L 6.4,11.6 L 12.8,4.6";
        public const string MoreDotsPath = "M 4,8 A 0.9,0.9 0 1 1 2.2,8 A 0.9,0.9 0 1 1 4,8 M 8.9,8 A 0.9,0.9 0 1 1 7.1,8 A 0.9,0.9 0 1 1 8.9,8 M 13.8,8 A 0.9,0.9 0 1 1 12,8 A 0.9,0.9 0 1 1 13.8,8";

        // Oriel Inline Caption Buttons (Buttons.tsx:20-25, 16x16 viewBox, shown at 12x12, sw=1.45)
        public const string WinMinimizePath = "M 2.6,8 L 13.4,8";
        public const string WinMaximizePath = "M 5.4,2.8 L 10.6,2.8 C 12,2.8 13.2,4 13.2,5.4 L 13.2,10.6 C 13.2,12 12,13.2 10.6,13.2 L 5.4,13.2 C 4,13.2 2.8,12 2.8,10.6 L 2.8,5.4 C 2.8,4 4,2.8 5.4,2.8 Z";
        public const string WinRestorePath = "M 4.6,5.2 L 8.6,5.2 C 9.8,5.2 10.8,6.2 10.8,7.4 L 10.8,11.4 C 10.8,12.6 9.8,13.6 8.6,13.6 L 4.6,13.6 C 3.4,13.6 2.4,12.6 2.4,11.4 L 2.4,7.4 C 2.4,6.2 3.4,5.2 4.6,5.2 Z M 5.2,5.2 L 5.2,4.8 C 5.2,3.6 6.2,2.6 7.4,2.6 L 11.4,2.6 C 12.6,2.6 13.6,3.6 13.6,4.8 L 13.6,8.8 C 13.6,10 12.6,11 11.4,11 L 10.8,11";
        public const string WinClosePath = "M 3.6,3.6 L 12.4,12.4 M 12.4,3.6 L 3.6,12.4";

        // All 24 Oriel Space Icons (16x16 viewBox, exact order & names)
        public static readonly string[] SpaceIconNames = new[]
        {
            "Work", "Office", "Desktop", "Laptop", "Code", "Terminal",
            "AI", "Thinking", "Ideas", "Games", "Leisure", "Café",
            "Music", "Film", "Art", "Photos", "Home", "Reading",
            "Studies", "Shopping", "Travel", "Sport", "Nature", "Personal"
        };

        public static string GetSpaceIconPath(string name)
        {
            switch (name ?? "Home")
            {
                case "Work": return "M 2.5,5.5 L 13.5,5.5 L 13.5,12.5 L 2.5,12.5 Z M 5.8,5.5 L 5.8,3.5 L 10.2,3.5 L 10.2,5.5 M 2.5,8.5 L 13.5,8.5";
                case "Office": return "M 3,13.5 L 3,3 L 9.5,3 L 9.5,13.5 M 9.5,6.5 L 13,6.5 L 13,13.5 M 2,13.5 L 14,13.5 M 5.2,5.5 L 5.2,6.5 M 7.3,5.5 L 7.3,6.5 M 5.2,8.5 L 5.2,9.5 M 7.3,8.5 L 7.3,9.5";
                case "Desktop": return "M 2.2,3.2 L 13.8,3.2 L 13.8,10.5 L 2.2,10.5 Z M 6,13.2 L 10,13.2 M 8,10.5 L 8,13.2";
                case "Laptop": return "M 3.2,4 L 12.8,4 L 12.8,10.5 L 3.2,10.5 Z M 1.6,12.5 L 14.4,12.5";
                case "Code": return "M 5.5,4.5 L 2,8 L 5.5,11.5 M 10.5,4.5 L 14,8 L 10.5,11.5 M 9,3.5 L 7,12.5";
                case "Terminal": return "M 2.2,3.2 L 13.8,3.2 L 13.8,12.8 L 2.2,12.8 Z M 4.8,6.2 L 7.2,8.2 L 4.8,10.2 M 8.2,10.2 L 11.2,10.2";
                case "AI": return "M 8,2 L 9.2,6.2 L 13.4,7.4 L 9.2,8.6 L 8,12.8 L 6.8,8.6 L 2.6,7.4 L 6.8,6.2 Z M 12.5,2.5 L 12.5,4.3 M 11.6,3.4 L 13.4,3.4";
                case "Thinking": return "M 8,13.5 L 8,11.5 C 10.8,11.5 12.8,9.6 12.8,7 C 12.8,4.4 10.6,2.5 8,2.5 C 5.4,2.5 3.2,4.4 3.2,7 C 3.2,8.6 4.1,10 5.5,10.8 L 5.5,13.5 M 6.2,6.5 A 1.2,1.2 0 1 1 8.6,6.5";
                case "Ideas": return "M 5.6,11.2 L 10.4,11.2 M 6.2,13.2 L 9.8,13.2 M 8,2.2 C 5.6,2.2 4,3.9 4,6.1 C 4,7.5 4.8,8.5 5.6,9.4 L 5.6,11.2 L 10.4,11.2 L 10.4,9.4 C 11.2,8.5 12,7.5 12,6.1 C 12,3.9 10.4,2.2 8,2.2 Z";
                case "Games": return "M 4.2,5.2 L 11.8,5.2 C 13,5.2 14,6.2 14,7.6 L 13.5,11.2 C 13.3,12.2 12.2,12.6 11.4,12 L 9.8,10.5 L 6.2,10.5 L 4.6,12 C 3.8,12.6 2.7,12.2 2.5,11.2 L 2,7.6 C 2,6.2 3,5.2 4.2,5.2 Z M 5.2,7.2 L 5.2,9.2 M 4.2,8.2 L 6.2,8.2 M 10.8,7.6 L 10.8,7.7 M 11.8,8.6 L 11.8,8.7";
                case "Leisure": return "M 8,2.5 C 4.5,2.5 2.2,5 2.2,7.5 L 13.8,7.5 C 13.8,5 11.5,2.5 8,2.5 Z M 8,7.5 L 8,13.2 M 5.5,13.2 L 10.5,13.2";
                case "Café": return "M 3,5.2 L 11,5.2 L 10.2,11.8 C 10.1,12.5 9.5,13 8.8,13 L 5.2,13 C 4.5,13 3.9,12.5 3.8,11.8 Z M 11,6.4 L 12.4,6.4 C 13.2,6.4 13.8,7 13.8,7.8 C 13.8,8.6 13.2,9.2 12.4,9.2 L 10.6,9.2 M 2.5,13.5 L 11.5,13.5";
                case "Music": return "M 6.2,11.6 L 6.2,4 L 12.6,2.8 L 12.6,10.4 M 4.6,11.6 A 1.6,1.6 0 1 0 6.2,11.6 M 11,10.4 A 1.6,1.6 0 1 0 12.6,10.4";
                case "Film": return "M 2.5,3.2 L 13.5,3.2 L 13.5,12.8 L 2.5,12.8 Z M 5.2,3.2 L 5.2,12.8 M 10.8,3.2 L 10.8,12.8 M 2.5,6.4 L 5.2,6.4 M 2.5,9.6 L 5.2,9.6 M 10.8,6.4 L 13.5,6.4 M 10.8,9.6 L 13.5,9.6";
                case "Art": return "M 8,2.2 C 4.6,2.2 2.2,4.8 2.2,8 C 2.2,11.2 4.8,13.6 8,13.6 C 9,13.6 9.6,13 9.6,12.2 C 9.6,11.6 9.2,11.2 9.2,10.8 C 9.2,10.2 9.7,9.8 10.4,9.8 L 11.4,9.8 C 12.8,9.8 13.8,8.8 13.8,7.4 C 13.8,4.4 11.2,2.2 8,2.2 Z M 5.5,6.5 L 5.5,6.6 M 8,5.2 L 8,5.3 M 10.5,6.5 L 10.5,6.6";
                case "Photos": return "M 2.2,5.2 L 5,5.2 L 6.2,3.4 L 9.8,3.4 L 11,5.2 L 13.8,5.2 L 13.8,12.6 L 2.2,12.6 Z M 8,6.8 A 2.2,2.2 0 1 0 8,11.2 A 2.2,2.2 0 1 0 8,6.8";
                case "Reading": return "M 8,4.2 C 6.6,3.2 4.4,3 2.4,3.6 L 2.4,12.2 C 4.4,11.6 6.6,11.8 8,12.8 C 9.4,11.8 11.6,11.6 13.6,12.2 L 13.6,3.6 C 11.6,3 9.4,3.2 8,4.2 Z M 8,4.2 L 8,12.8";
                case "Studies": return "M 1.8,6.5 L 8,3.4 L 14.2,6.5 L 8,9.6 Z M 4.2,7.8 L 4.2,11.2 C 5.4,12.2 6.8,12.6 8,12.6 C 9.2,12.6 10.6,12.2 11.8,11.2 L 11.8,7.8 M 13.8,6.8 L 13.8,10.8";
                case "Shopping": return "M 2,3.2 L 3.8,3.2 L 5.2,10.2 L 12.2,10.2 L 13.6,5.2 L 4.4,5.2 M 6,12.8 A 0.8,0.8 0 1 0 6,12.9 M 11.4,12.8 A 0.8,0.8 0 1 0 11.4,12.9";
                case "Travel": return "M 13.6,2.4 L 9.2,13.6 L 7.6,8.8 L 2.4,7.2 Z M 7.6,8.8 L 13.6,2.4";
                case "Sport": return "M 2.6,6.2 L 2.6,9.8 M 4.4,5 L 4.4,11 M 11.6,5 L 11.6,11 M 13.4,6.2 L 13.4,9.8 M 4.4,8 L 11.6,8";
                case "Nature": return "M 13.2,2.8 C 7.5,2.8 3.2,6 3.2,11.5 L 3.2,13.2 L 4.8,13.2 C 10.4,13.2 13.2,9 13.2,2.8 Z M 3.2,13.2 L 9.5,6.8";
                case "Personal": return "M 8,13.2 L 3.4,8.6 C 2,7.2 2,4.8 3.5,3.5 C 4.9,2.4 6.9,2.8 8,4.3 C 9.1,2.8 11.1,2.4 12.5,3.5 C 14,4.8 14,7.2 12.6,8.6 Z";
                default: return HomePath;
            }
        }

        public static UIElement Draw(string pathData, Brush stroke, double size = 12, double strokeThickness = 1.45)
        {
            var path = new Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.None,
                Width = 16,
                Height = 16
            };

            return new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = path
            };
        }

        public static UIElement DrawFilled(string pathData, Brush fill, double size = 12)
        {
            var path = new Path
            {
                Data = Geometry.Parse(pathData),
                Fill = fill,
                Stroke = fill,
                StrokeThickness = 1.1,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.None,
                Width = 16,
                Height = 16
            };

            return new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = path
            };
        }
    }
}
