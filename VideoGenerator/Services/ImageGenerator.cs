using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Utils;

// Resolve ambiguities with aliases
using Color = SixLabors.ImageSharp.Color;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using RectangleF = SixLabors.ImageSharp.RectangleF;
using Point = SixLabors.ImageSharp.Point;
using PointF = SixLabors.ImageSharp.PointF;
using Size = SixLabors.ImageSharp.Size;
using Image = SixLabors.ImageSharp.Image;
using FontStyle = SixLabors.Fonts.FontStyle;
using HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;

namespace VideoGenerator.Services
{
    public class ImageGenerator
    {
        private Image<Rgba32> _cachedBackground = null;

        public ImageGenerator()
        {
        }

        private async Task<Image<Rgba32>> LoadBackgroundAsync(string customPath = null)
        {
            if (customPath != null && File.Exists(customPath))
            {
                return await Image.LoadAsync<Rgba32>(customPath);
            }

            if (_cachedBackground != null)
            {
                return _cachedBackground.Clone();
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "VideoGenerator.Resources.DefaultBackground.jpg";

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                _cachedBackground = await Image.LoadAsync<Rgba32>(stream);
                return _cachedBackground.Clone();
            }

            if (File.Exists(AppConfig.BackgroundPath))
            {
                _cachedBackground = await Image.LoadAsync<Rgba32>(AppConfig.BackgroundPath);
                return _cachedBackground.Clone();
            }

            // Professional dark fallback
            _cachedBackground = new Image<Rgba32>(HudTextures.CanvasWidth, HudTextures.CanvasHeight, Color.ParseHex("#0D0D0D"));
            return _cachedBackground.Clone();
        }

        public async Task<string> CreateImageAsync(ParsedEvent eventData, string fontName = "Arial", string customBackgroundPath = null, float textVerticalOffset = 0f)
        {
            Directory.CreateDirectory(AppConfig.OutputImagesDir);
            string outputPath = Path.Combine(AppConfig.OutputImagesDir, $"{eventData.OriginalFolder}.png");
            var bytes = await CreateImageBytesAsync(eventData, fontName, customBackgroundPath, textVerticalOffset);
            if (bytes == null) return null;

            await File.WriteAllBytesAsync(outputPath, bytes);
            return outputPath;
        }

        public async Task<byte[]> CreateImageBytesAsync(ParsedEvent eventData, string fontName = "Arial", string customBackgroundPath = null, float textVerticalOffset = 0f)
        {
            try
            {
                // 1. Load and prepare background
                using var loadedImage = await LoadBackgroundAsync(customBackgroundPath);
                var image = new Image<Rgba32>(HudTextures.CanvasWidth, HudTextures.CanvasHeight, Color.Black);
                
                loadedImage.Mutate(x => x.Resize(new ResizeOptions { 
                    Size = new Size(HudTextures.CanvasWidth, HudTextures.CanvasHeight), 
                    Mode = ResizeMode.Crop 
                }));
                image.Mutate(x => x.DrawImage(loadedImage, 1f));

                // 2. Draw HUD Ribbon (Semi-transparent with White Outline)
                var ribbonRect = new Rectangle(-5, HudTextures.RibbonY, HudTextures.CanvasWidth + 10, HudTextures.RibbonHeight);
                image.Mutate(x => x.Fill(HudTextures.RibbonBackgroundColor, ribbonRect));
                image.Mutate(x => x.Draw(Color.White, 2f, ribbonRect));

                // 3. Draw Text
                if (!SixLabors.Fonts.SystemFonts.TryGet(fontName, out var fontFamily))
                    fontFamily = SixLabors.Fonts.SystemFonts.Families.First();

                var font = fontFamily.CreateFont(HudTextures.DefaultFontSize, FontStyle.Bold);
                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(HudTextures.CanvasWidth / 2, HudTextures.TextAnchorY + textVerticalOffset)
                };
                image.Mutate(x => x.DrawText(textOptions, eventData.DisplayText, HudTextures.TextWhite));

                // 4. Draw Icon (Above the Ribbon)
                if (!string.IsNullOrEmpty(eventData.IconPath) && File.Exists(eventData.IconPath))
                {
                    int iconY = HudTextures.IconY;
                    var borderRect = new Rectangle(HudTextures.IconX - 2, iconY - 2, HudTextures.IconSize + 4, HudTextures.IconSize + 4);

                    using var icon = await Image.LoadAsync<Rgba32>(eventData.IconPath);
                    
                    // IF ICON IS SPLASH (RECTANGULAR), DO SQUARE CROP
                    if (icon.Width > icon.Height * 1.2) // Typical splash ratio
                    {
                        int minDim = Math.Min(icon.Width, icon.Height);
                        icon.Mutate(x => x.Crop(new Rectangle((icon.Width - minDim) / 2, 0, minDim, minDim)));
                    }

                    icon.Mutate(x => x.Resize(HudTextures.IconSize - 4, HudTextures.IconSize - 4));
                    
                    image.Mutate(x => x.Fill(Color.White, borderRect));
                    image.Mutate(x => x.DrawImage(icon, new Point(HudTextures.IconX, iconY), 1f));
                }

                // 5. Global 1px border
                image.Mutate(x => x.Draw(Color.White, 1f, new Rectangle(0, 0, 1919, 1079)));

                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                return ms.ToArray();
            }
            catch { return null; }
        }
    }
}
