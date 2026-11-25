using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SearchBrowser
{
    // Exact vector paths ported from Oriel's src/ui/lib/icons.tsx & src/ui/chrome/Buttons.tsx
    public static class VectorIcons
    {
        public const string BackPath = "M 10,3.5 L 5.5,8 L 10,12.5";
        public const string ForwardPath = "M 6,3.5 L 10.5,8 L 6,12.5";
        public const string ReloadPath = "M 12.6,6.4 A 4.9,4.9 0 1 0 12.8,9 M 12.9,3.4 L 12.9,6.6 L 9.7,6.6";
        public const string PlusPath = "M 8,3.5 L 8,12.5 M 3.5,8 L 12.5,8";
        public const string CloseSmallPath = "M 5,5 L 11,11 M 11,5 L 5,11";
        public const string SearchPath = "M 11.2,7 A 4.2,4.2 0 1 1 2.8,7 A 4.2,4.2 0 1 1 11.2,7 M 10.2,10.2 L 13.5,13.5";
        public const string SidebarPath = "M 4.5,3 L 11.5,3 C 12.6,3 13.5,3.9 13.5,5 L 13.5,11 C 13.5,12.1 12.6,13 11.5,13 L 4.5,13 C 3.4,13 2.5,12.1 2.5,11 L 2.5,5 C 2.5,3.9 3.4,3 4.5,3 Z M 6.3,3 L 6.3,13";
        public const string SplitPath = "M 4.5,3 L 11.5,3 C 12.6,3 13.5,3.9 13.5,5 L 13.5,11 C 13.5,12.1 12.6,13 11.5,13 L 4.5,13 C 3.4,13 2.5,12.1 2.5,11 L 2.5,5 C 2.5,3.9 3.4,3 4.5,3 Z M 8,3 L 8,13";
        public const string PuzzlePath = "M 6,2.8 A 1.4,1.4 0 0 1 8.8,2.8 L 8.8,3.8 L 11.4,3.8 L 11.4,6.4 L 12.4,6.4 A 1.4,1.4 0 0 1 12.4,9.2 L 11.4,9.2 L 11.4,12.4 L 8.6,12.4 L 8.6,11.4 A 1.4,1.4 0 0 0 5.8,11.4 L 5.8,12.4 L 2.6,12.4 L 2.6,9.2 L 3.6,9.2 A 1.4,1.4 0 0 0 3.6,6.4 L 2.6,6.4 L 2.6,3.8 L 6,3.8 Z";
        public const string FolderPath = "M 2.5,5.2 C 2.5,4.3 3.2,3.6 4.1,3.6 L 6.8,3.6 L 8.2,5.1 L 11.9,5.1 C 12.8,5.1 13.5,5.8 13.5,6.7 L 13.5,11.2 C 13.5,12.1 12.8,12.8 11.9,12.8 L 4.1,12.8 C 3.2,12.8 2.5,12.1 2.5,11.2 Z";
        public const string BookmarkPath = "M 4.6,3.4 C 4.6,2.74 5.14,2.2 5.8,2.2 L 10.2,2.2 C 10.86,2.2 11.4,2.74 11.4,3.4 L 11.4,13.6 L 8,11.1 L 4.6,13.6 Z";
        public const string HomePath = "M 2.5,7.8 L 8,3 L 13.5,7.8 L 13.5,12.5 C 13.5,13.05 13.05,13.5 12.5,13.5 L 3.5,13.5 C 2.95,13.5 2.5,13.05 2.5,12.5 Z";
        public const string EyeOffPath = "M 2.2,8 C 3.8,5 5.8,3.8 8,3.8 C 10.2,3.8 12.2,5 13.8,8 C 12.2,11 10.2,12.2 8,12.2 C 5.8,12.2 3.8,11 2.2,8 Z M 3,3 L 13,13";
        public const string MoreDotsPath = "M 4,8 A 0.9,0.9 0 1 1 2.2,8 A 0.9,0.9 0 1 1 4,8 M 8.9,8 A 0.9,0.9 0 1 1 7.1,8 A 0.9,0.9 0 1 1 8.9,8 M 13.8,8 A 0.9,0.9 0 1 1 12,8 A 0.9,0.9 0 1 1 13.8,8";

        // Oriel Inline Caption Buttons (16x16 viewBox, shown at 12x12)
        public const string WinMinimizePath = "M 2.6,8 L 13.4,8";
        public const string WinMaximizePath = "M 5.4,2.8 L 10.6,2.8 C 12,2.8 13.2,4 13.2,5.4 L 13.2,10.6 C 13.2,12 12,13.2 10.6,13.2 L 5.4,13.2 C 4,13.2 2.8,12 2.8,10.6 L 2.8,5.4 C 2.8,4 4,2.8 5.4,2.8 Z";
        public const string WinClosePath = "M 3.6,3.6 L 12.4,12.4 M 12.4,3.6 L 3.6,12.4";

        public static UIElement Draw(string pathData, Brush stroke, double size = 14, double strokeThickness = 1.45)
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
    }
}
