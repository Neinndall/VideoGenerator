using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using VideoGenerator.Models;
using SixLabors.ImageSharp;

namespace VideoGenerator.Views
{
    public partial class BackgroundDesignView : UserControl
    {
        private readonly BackgroundModel _model = new();
        private readonly DataFetcher _dataFetcher;
        private readonly IconManager _iconManager;
        private readonly ImageGenerator _imageGenerator;
        private CancellationTokenSource _previewCts;

        public BackgroundDesignView(
            DataFetcher dataFetcher,
            IconManager iconManager,
            ImageGenerator imageGenerator)
        {
            InitializeComponent();
            _dataFetcher = dataFetcher;
            _iconManager = iconManager;
            _imageGenerator = imageGenerator;
            
            DataContext = _model;

            // Load System Fonts
            foreach (var family in SixLabors.Fonts.SystemFonts.Families)
                _model.FontNames.Add(family.Name);

            if (_model.FontNames.Count > 0)
            {
                string savedFont = AppSettings.Instance.SelectedFontName;
                if (!string.IsNullOrEmpty(savedFont) && _model.FontNames.Contains(savedFont))
                {
                    _model.SelectedFontName = savedFont;
                }
                else
                {
                    _model.SelectedFontName = _model.FontNames.Contains("Segoe UI") ? "Segoe UI" : _model.FontNames[0];
                }
            }

            // Initialize radio buttons
            if (AppSettings.Instance.IconAlignment.Equals("Right", StringComparison.OrdinalIgnoreCase))
                AlignRightRadio.IsChecked = true;
            else
                AlignLeftRadio.IsChecked = true;

            _model.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(_model.PreviewText))
                    DebouncedUpdatePreview();
                else if (e.PropertyName == nameof(_model.SelectedFontName))
                {
                    AppSettings.Instance.SelectedFontName = _model.SelectedFontName;
                    DebouncedUpdatePreview();
                }
            };

            AppSettings.Instance.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(AppSettings.CustomBackgroundPath) || 
                    e.PropertyName == nameof(AppSettings.BackgroundBrightness) ||
                    e.PropertyName == nameof(AppSettings.BackgroundContrast) ||
                    e.PropertyName == nameof(AppSettings.BackgroundSaturate) ||
                    e.PropertyName == nameof(AppSettings.TextVerticalOffset) ||
                    e.PropertyName == nameof(AppSettings.IconAlignment) ||
                    e.PropertyName == nameof(AppSettings.IconVerticalOffset) ||
                    e.PropertyName == nameof(AppSettings.SelectedFontName) ||
                    e.PropertyName == nameof(AppSettings.BubbleTextSize) ||
                    e.PropertyName == nameof(AppSettings.BubbleHeight) ||
                    e.PropertyName == nameof(AppSettings.BubbleOpacity) ||
                    e.PropertyName == nameof(AppSettings.BubbleVerticalOffset) ||
                    e.PropertyName == nameof(AppSettings.BubbleWidth) ||
                    e.PropertyName == nameof(AppSettings.BubbleHorizontalOffset) ||
                    e.PropertyName == nameof(AppSettings.BubbleBorderColor) ||
                    e.PropertyName == nameof(AppSettings.IconBorderColor) ||
                    e.PropertyName == nameof(AppSettings.IconBorderThickness) ||
                    e.PropertyName == nameof(AppSettings.BubbleBorderThickness))
                {
                    if (e.PropertyName == nameof(AppSettings.SelectedFontName) && _model.SelectedFontName != AppSettings.Instance.SelectedFontName)
                    {
                        _model.SelectedFontName = AppSettings.Instance.SelectedFontName;
                    }
                    DebouncedUpdatePreview();
                }
            };

            UpdatePreview();
        }

        private void AlignLeft_Checked(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Instance != null)
                AppSettings.Instance.IconAlignment = "Left";
        }

        private void AlignRight_Checked(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Instance != null)
                AppSettings.Instance.IconAlignment = "Right";
        }

        private void SelectBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                Title = "Select Background Image",
                Filters = { new CommonFileDialogFilter("Images", "*.jpg;*.jpeg;*.png;*.bmp") }
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                AppSettings.Instance.CustomBackgroundPath = dialog.FileName;
                UpdatePreview(); // Force immediate update
            }
        }

        private void RemoveBackground_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.CustomBackgroundPath = null;
            UpdatePreview();
        }

        private async void SearchSplash_Click(object sender, RoutedEventArgs e)
        {
            string query = SplashSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            try
            {
                string championName = query;
                string skinNumber = "0";

                if (query.Contains("_"))
                {
                    var parts = query.Split('_');
                    championName = parts[0];
                    skinNumber = parts[1];
                }

                string formatted = championName.Replace(" ", "").Replace("'", "");
                string url = $"https://ddragon.leagueoflegends.com/cdn/img/champion/splash/{formatted}_{skinNumber}.jpg";
                
                string path = await _dataFetcher.DownloadIconAsync(url, "splash");
                if (path != null) 
                {
                    AppSettings.Instance.CustomBackgroundPath = path;
                    UpdatePreview();
                }
            }
            catch { }
        }

        private void SplashSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SearchSplash_Click(sender, new RoutedEventArgs());
            }
        }

        private void DebouncedUpdatePreview()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            Task.Delay(50, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Dispatcher.Invoke(() => UpdatePreview());
            }, token);
        }

        private async void UpdatePreview()
        {
            try
            {
                // Ensure a dummy icon exists in the local cache for the designer preview
                string dummyIconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGenerator", "Cache", "preview_icon_placeholder.png");
                if (!File.Exists(dummyIconPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dummyIconPath));
                        using (var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(180, 180, SixLabors.ImageSharp.Color.ParseHex("#8B5CF6")))
                        {
                            img.SaveAsPng(dummyIconPath);
                        }
                    }
                    catch { }
                }

                var mockEvent = new ParsedEvent
                {
                    DisplayText = _model.PreviewText,
                    IconType = "champion",
                    IconLookupName = "Placeholder",
                    IconPath = File.Exists(dummyIconPath) ? dummyIconPath : null,
                    Dialogue = "Sample dialogue subtitle text to preview Hextech speech bubble style customization."
                };

                // Perform generation in background thread
                byte[] imageBytes = await Task.Run(() => 
                    _imageGenerator.CreateImageBytesAsync(mockEvent, _model.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset));

                if (imageBytes != null)
                {
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze(); // Essential for cross-thread access and performance
                    _model.PreviewImage = bitmap;
                }
            }
            catch { }
        }
        private bool _isMaximized = false;
        private void RenderCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                _isMaximized = !_isMaximized;
                InspectorColumn.Width = _isMaximized ? new GridLength(0) : new GridLength(380);
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reset all visual layout parameters to their default values?", "Reset Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                AppSettings.Instance.TextVerticalOffset = -8f;
                AppSettings.Instance.BackgroundBrightness = 1.0f;
                AppSettings.Instance.BackgroundContrast = 1.0f;
                AppSettings.Instance.BackgroundSaturate = 1.0f;
                AppSettings.Instance.IconVerticalOffset = 0f;
                AppSettings.Instance.IconAlignment = "Left";
                AppSettings.Instance.SelectedFontName = "Segoe UI";
                AppSettings.Instance.BubbleTextSize = 22f;
                AppSettings.Instance.BubbleHeight = 120f;
                AppSettings.Instance.BubbleOpacity = 0.85f;
                AppSettings.Instance.BubbleVerticalOffset = 0f;
                AppSettings.Instance.BubbleWidth = 900f;
                AppSettings.Instance.BubbleHorizontalOffset = 0f;
                AppSettings.Instance.BubbleBorderColor = "#C89B3C";
                AppSettings.Instance.IconBorderColor = "#C89B3C";
                AppSettings.Instance.IconBorderThickness = 2f;
                AppSettings.Instance.BubbleBorderThickness = 2f;
                
                AlignLeftRadio.IsChecked = true;
                _model.SelectedFontName = "Segoe UI";
                UpdatePreview();
            }
        }
    }
}
