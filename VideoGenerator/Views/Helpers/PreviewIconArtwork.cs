using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;

namespace VideoGenerator.Views.Helpers
{
    /// <summary>
    /// Preview-only artwork. The real pipeline always uses the event's own icon.
    /// Renders a Material Design vector glyph to a transparent PNG with a dark
    /// navy tile, so the design editor never needs a flat placeholder bitmap.
    /// </summary>
    internal static class PreviewIconArtwork
    {
        public static string EnsureCreated(string path)
        {
            if (File.Exists(path)) return path;

            const int size = 360; // 2x resolution for crisp downscaling to HUD size.
            var background = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#17283D"),
                (Color)ColorConverter.ConvertFromString("#0C1422"),
                new Point(0, 0), new Point(1, 1));

            var icon = new PackIcon
            {
                Kind = PackIconKind.CommentTextOutline,
                Width = 138,
                Height = 138,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8E8FF")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var center = new Border
            {
                Width = 206,
                Height = 206,
                CornerRadius = new CornerRadius(103),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#20354C")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#31516D")),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = icon
            };

            var tile = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(28),
                Background = background,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34506A")),
                BorderThickness = new Thickness(3),
                Child = center
            };
            var root = new Grid
            {
                Width = size,
                Height = size,
                Background = Brushes.Transparent,
                Children = { tile }
            };

            root.Measure(new Size(size, size));
            root.Arrange(new Rect(0, 0, size, size));
            root.UpdateLayout();

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string directory = Path.GetDirectoryName(path)
                ?? throw new ArgumentException("A full preview asset path is required.", nameof(path));
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporary))
                    encoder.Save(stream);
                File.Move(temporary, path, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return path;
        }
    }
}
