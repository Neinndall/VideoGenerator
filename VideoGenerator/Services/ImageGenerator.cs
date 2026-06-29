using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
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
        private string _cachedCustomBackgroundPath = null;
        private Image<Rgba32> _cachedCustomBackground = null;

        private string _cachedFontName = null;
        private FontFamily _cachedFontFamily;

        private readonly Dictionary<string, Image<Rgba32>> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        public ImageGenerator()
        {
        }

        private FontFamily GetFontFamily(string fontName)
        {
            if (_cachedFontName == fontName && _cachedFontFamily != default)
            {
                return _cachedFontFamily;
            }

            if (!SixLabors.Fonts.SystemFonts.TryGet(fontName, out var fontFamily))
                fontFamily = SixLabors.Fonts.SystemFonts.Families.First();

            _cachedFontName = fontName;
            _cachedFontFamily = fontFamily;
            return fontFamily;
        }

        private async Task<Image<Rgba32>> LoadBackgroundAsync(string customPath = null)
        {
            if (customPath != null && File.Exists(customPath))
            {
                if (_cachedCustomBackgroundPath == customPath && _cachedCustomBackground != null)
                {
                    return _cachedCustomBackground.Clone();
                }

                try
                {
                    var loaded = await Image.LoadAsync<Rgba32>(customPath);
                    _cachedCustomBackground?.Dispose();
                    _cachedCustomBackground = loaded;
                    _cachedCustomBackgroundPath = customPath;
                    return _cachedCustomBackground.Clone();
                }
                catch
                {
                    // Fallback to default if load fails
                }
            }

            if (_cachedBackground != null)
            {
                return _cachedBackground.Clone();
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "VideoGenerator.Resources.DefaultBackground.png";

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

        public async Task<string> CreateImageAsync(ParsedEvent eventData, string fontName = "Arial", string customBackgroundPath = null, float textVerticalOffset = 0f, string customSuffix = "", string subFolder = "")
        {
            string targetDir = string.IsNullOrEmpty(subFolder) ? AppConfig.OutputImagesDir : Path.Combine(AppConfig.OutputImagesDir, subFolder);
            Directory.CreateDirectory(targetDir);
            string filename = string.IsNullOrEmpty(customSuffix) ? $"{eventData.OriginalFolder}.png" : $"{eventData.OriginalFolder}_{customSuffix}.png";
            string outputPath = Path.Combine(targetDir, filename);
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

                // Apply brightness, contrast and saturation settings
                float brightness = AppSettings.Instance.BackgroundBrightness;
                float contrast = AppSettings.Instance.BackgroundContrast;
                float saturate = AppSettings.Instance.BackgroundSaturate;
                loadedImage.Mutate(x => {
                    x.Brightness(brightness);
                    x.Contrast(contrast);
                    x.Saturate(saturate);
                });

                image.Mutate(x => x.DrawImage(loadedImage, 1f));

                // 2. Draw HUD Ribbon (Semi-transparent background, no outline to prevent clashing with pre-designed default backgrounds)
                var ribbonRect = new Rectangle(-5, HudTextures.RibbonY, HudTextures.CanvasWidth + 10, HudTextures.RibbonHeight);
                image.Mutate(x => x.Fill(HudTextures.RibbonBackgroundColor, ribbonRect));

                // 3. Draw Text
                var fontFamily = GetFontFamily(fontName);

                var font = fontFamily.CreateFont(HudTextures.DefaultFontSize, FontStyle.Bold);
                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(HudTextures.CanvasWidth / 2, HudTextures.TextAnchorY + textVerticalOffset)
                };
                image.Mutate(x => x.DrawText(textOptions, eventData.DisplayText, HudTextures.TextWhite));

                // 3.5 Draw Dialogue Speech Bubble if present
                if (AppSettings.Instance.EnableTranscriptions && eventData != null && !string.IsNullOrEmpty(eventData.Dialogue))
                {
                    float fontSize = AppSettings.Instance.BubbleTextSize;
                    var dialogueFont = fontFamily.CreateFont(fontSize, FontStyle.Bold);
                    
                    // Style Colors
                    byte alpha = (byte)(Math.Clamp(AppSettings.Instance.BubbleOpacity, 0f, 1f) * 255);
                    var bubbleBgColor = Color.FromRgba(10, 10, 12, alpha); // Hextech dark transparent with customizable opacity
                    
                    Color bubbleBorderColor;
                    try
                    {
                        bubbleBorderColor = Color.ParseHex(AppSettings.Instance.BubbleBorderColor ?? "#C89B3C");
                    }
                    catch
                    {
                        bubbleBorderColor = Color.ParseHex("#C89B3C");
                    }

                    // Check if there is a valid icon drawn
                    bool hasIcon = !string.IsNullOrEmpty(eventData.IconPath) && File.Exists(eventData.IconPath);
                    bool isRightAlign = AppSettings.Instance.IconAlignment.Equals("Right", StringComparison.OrdinalIgnoreCase);
                    
                    int bubbleWidth = (int)AppSettings.Instance.BubbleWidth;
                    int bubbleHeight = (int)AppSettings.Instance.BubbleHeight;
                    // If icon exists, center align with the icon vertically (default 738, customizable offset).
                    // If no icon, place it lower, sitting 20px above the ribbon (default 758, customizable offset).
                    int bubbleY = (int)((hasIcon ? 738 : 758) + AppSettings.Instance.BubbleVerticalOffset);
                    int bubbleX;

                    if (hasIcon)
                    {
                        // Smart positioning: if right-aligned, the bubble expands inwards to avoid clashing with the icon (which starts at 1690).
                        // If left-aligned, it starts at 260.
                        bubbleX = (isRightAlign ? (1660 - bubbleWidth) : 260) + (int)AppSettings.Instance.BubbleHorizontalOffset;
                    }
                    else
                    {
                        // Centered horizontally if there is no icon
                        bubbleX = ((HudTextures.CanvasWidth - bubbleWidth) / 2) + (int)AppSettings.Instance.BubbleHorizontalOffset;
                    }

                    // 1. Draw the Bubble Rectangle
                    var bubbleRect = new RectangleF(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
                    image.Mutate(x => x.Fill(bubbleBgColor, bubbleRect));
                    float bubbleThickness = AppSettings.Instance.BubbleBorderThickness;
                    if (bubbleThickness > 0)
                    {
                        image.Mutate(x => x.Draw(bubbleBorderColor, bubbleThickness, bubbleRect));
                    }

                    // 2. Draw the Triangle Tail pointing to the icon ONLY if the icon is present
                    if (hasIcon)
                    {
                        PointF[] tailPoints;
                        if (isRightAlign)
                        {
                            // Points to the left edge of the right icon (which starts at 1690)
                            tailPoints = new PointF[]
                            {
                                new PointF(bubbleX + bubbleWidth, bubbleY + (bubbleHeight * 0.3f)),
                                new PointF(1680f, bubbleY + (bubbleHeight * 0.5f)), // Tip of the tail pointing right
                                new PointF(bubbleX + bubbleWidth, bubbleY + (bubbleHeight * 0.7f))
                            };
                        }
                        else
                        {
                            // Points to the right edge of the left icon (which ends at 230)
                            tailPoints = new PointF[]
                            {
                                new PointF(bubbleX, bubbleY + (bubbleHeight * 0.3f)),
                                new PointF(240f, bubbleY + (bubbleHeight * 0.5f)),  // Tip of the tail pointing left
                                new PointF(bubbleX, bubbleY + (bubbleHeight * 0.7f))
                            };
                        }

                        // Fill and outline the tail
                        image.Mutate(x => x.FillPolygon(bubbleBgColor, tailPoints));
                        if (bubbleThickness > 0)
                        {
                            image.Mutate(x => x.DrawPolygon(bubbleBorderColor, bubbleThickness, tailPoints));
                        }
                    }

                    // 3. Draw Dialogue Text (Wrapped inside the bubble)
                    var textPadding = 24f;
                    var dialogueOptions = new RichTextOptions(dialogueFont)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Origin = new PointF(bubbleX + (bubbleWidth / 2), bubbleY + (bubbleHeight / 2)),
                        WrappingLength = bubbleWidth - (textPadding * 2)
                    };
                    image.Mutate(x => x.DrawText(dialogueOptions, eventData.Dialogue, Color.White));
                }

                // 4. Draw Icon (Above the Ribbon)
                if (!string.IsNullOrEmpty(eventData.IconPath) && File.Exists(eventData.IconPath))
                {
                    // Calculate horizontal position based on alignment
                    int iconX = HudTextures.IconX; // Default Left = 50
                    if (AppSettings.Instance.IconAlignment.Equals("Right", StringComparison.OrdinalIgnoreCase))
                    {
                        iconX = HudTextures.CanvasWidth - HudTextures.IconSize - HudTextures.IconX; // 1920 - 180 - 50 = 1690
                    }

                    // Calculate vertical position including offset
                    int iconY = (int)(HudTextures.IconY + AppSettings.Instance.IconVerticalOffset);

                    var borderRect = new Rectangle(iconX - 2, iconY - 2, HudTextures.IconSize + 4, HudTextures.IconSize + 4);

                    Image<Rgba32> iconToDraw = null;
                    lock (_iconCache)
                    {
                        if (_iconCache.TryGetValue(eventData.IconPath, out var cachedIcon))
                        {
                            iconToDraw = cachedIcon.Clone();
                        }
                    }

                    if (iconToDraw == null)
                    {
                        using var icon = await Image.LoadAsync<Rgba32>(eventData.IconPath);
                        
                        // IF ICON IS SPLASH (RECTANGULAR), DO SQUARE CROP
                        if (icon.Width > icon.Height * 1.2) // Typical splash ratio
                        {
                            int minDim = Math.Min(icon.Width, icon.Height);
                            icon.Mutate(x => x.Crop(new Rectangle((icon.Width - minDim) / 2, 0, minDim, minDim)));
                        }

                        icon.Mutate(x => x.Resize(HudTextures.IconSize - 4, HudTextures.IconSize - 4));
                        
                        lock (_iconCache)
                        {
                            _iconCache[eventData.IconPath] = icon.Clone();
                        }
                        iconToDraw = icon.Clone();
                    }

                    using (iconToDraw)
                    {
                        image.Mutate(x => x.DrawImage(iconToDraw, new Point(iconX, iconY), 1f));
                    }

                    // Draw configurable Icon Border
                    float thickness = AppSettings.Instance.IconBorderThickness;
                    if (thickness > 0)
                    {
                        Color iconBorderColor;
                        try
                        {
                            iconBorderColor = Color.ParseHex(AppSettings.Instance.IconBorderColor ?? "#C89B3C");
                        }
                        catch
                        {
                            iconBorderColor = Color.ParseHex("#C89B3C");
                        }

                        var iconRect = new RectangleF(iconX, iconY, HudTextures.IconSize, HudTextures.IconSize);
                        image.Mutate(x => x.Draw(iconBorderColor, thickness, iconRect));
                    }
                }

                // Mask the outer edges of the 1920x1080 canvas to cover any interpolation/resize edge bleed
                image.Mutate(x => x.Draw(Color.Black, 4f, new Rectangle(0, 0, HudTextures.CanvasWidth, HudTextures.CanvasHeight)));

                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                return ms.ToArray();
            }
            catch { return null; }
        }
    }
}
