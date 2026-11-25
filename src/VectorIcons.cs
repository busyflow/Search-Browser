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
