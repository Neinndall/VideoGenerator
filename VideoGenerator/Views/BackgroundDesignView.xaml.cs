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

            _model.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(_model.PreviewText))
                    DebouncedUpdatePreview();
            };

            AppSettings.Instance.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(AppSettings.CustomBackgroundPath) || e.PropertyName == nameof(AppSettings.TextVerticalOffset))
                    DebouncedUpdatePreview();
            };

            UpdatePreview();
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
                var mockEvent = new ParsedEvent
                {
                    DisplayText = _model.PreviewText,
                    IconType = "generic", // Force placeholder in preview for speed and layout check
                    IconLookupName = "Placeholder"
                };

                // Perform generation in background thread
                byte[] imageBytes = await Task.Run(() => 
                    _imageGenerator.CreateImageBytesAsync(mockEvent, "Segoe UI", AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset));

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
    }
}
