using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using VideoGenerator.Views.Dialogs;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Helpers;

namespace VideoGenerator.Views
{
    public partial class BackgroundDesignView : UserControl
    {
        private readonly BackgroundModel _model = new();
        private readonly LocalGameIconManager _iconManager;
        private readonly LocalGameDataRefreshService _gameDataRefresh;
        private readonly ImageGenerator _imageGenerator;
        private CancellationTokenSource _previewCts;
        private long _previewGeneration;
        private bool _splashSearchBusy;
        private string _splashSearchChampion;

        public BackgroundDesignView(
            LocalGameIconManager iconManager,
            ImageGenerator imageGenerator,
            LocalGameDataRefreshService gameDataRefresh)
        {
            InitializeComponent();
            _iconManager = iconManager;
            _imageGenerator = imageGenerator;
            _gameDataRefresh = gameDataRefresh;
            
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
            if (_splashSearchBusy) return;
            SplashResultsPanel.Visibility = Visibility.Collapsed;
            if (!LeagueSplashQueryParser.TryParse(SplashSearchBox.Text, out var query))
            {
                SetSplashSearchStatus("Enter a champion (Aatrox), a skin (Aatrox_1 or Aatrox1), or several skins (Aatrox1,2).");
                return;
            }
            _splashSearchBusy = true;
            SplashSearchButton.IsEnabled = false;
            SplashSearchBox.IsEnabled = false;
            SetSplashSearchStatus("Checking installed game artwork…");
            try
            {
                // A new game patch may have landed after the current session
                // started. Refresh local WAD catalogs before resolving its skin.
                await _gameDataRefresh.RefreshAsync();

                if (query.SkinIds.Count == 1 &&
                    await ApplySplashAsync(query.Champion, query.SkinIds[0]))
                {
                    return;
                }

                IReadOnlyList<LocalGameIconManager.LocalSplashSkin> available =
                    await _iconManager.GetAvailableSplashSkinsAsync(query.Champion);
                if (available.Count == 0)
                {
                    SetSplashSearchStatus("No splash artwork for this champion was found in the installed WADs. Check Settings → General & Media and restart if the League directory was just changed.");
                    return;
                }

                if (query.HasSkinIds)
                    available = available.Where(skin => query.SkinIds.Contains(skin.SkinId)).ToArray();

                if (available.Count == 0)
                {
                    SetSplashSearchStatus("Those skin numbers do not have a local splash in this installation. Enter the champion name alone to see available skins.");
                    return;
                }

                _splashSearchChampion = query.Champion;
                SplashResultsList.SelectedItem = null;
                SplashResultsList.ItemsSource = available;
                SplashResultsPanel.Visibility = Visibility.Visible;
                SetSplashSearchStatus($"Select a skin ({available.Count} available). Only full-size local splashes are listed.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Local splash search failed: {ex}");
                SetSplashSearchStatus("Unable to read the local WAD splash catalog. Check the League installation in Settings → General & Media.");
            }
            finally
            {
                _splashSearchBusy = false;
                SplashSearchButton.IsEnabled = true;
                SplashSearchBox.IsEnabled = true;
            }
        }

        private void SplashSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SplashResultsPanel == null || SplashSearchStatusText == null) return;
            SplashResultsPanel.Visibility = Visibility.Collapsed;
            SplashSearchStatusText.Visibility = Visibility.Collapsed;
        }

        private async void SplashResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_splashSearchBusy ||
                SplashResultsList.SelectedItem is not LocalGameIconManager.LocalSplashSkin option)
                return;
            SplashResultsList.SelectedItem = null;
            _splashSearchBusy = true;
            SplashSearchButton.IsEnabled = false;
            SplashSearchBox.IsEnabled = false;
            SetSplashSearchStatus($"Loading skin {option.SkinId}…");
            try
            {
                if (!await ApplySplashAsync(_splashSearchChampion, option.SkinId))
                    SetSplashSearchStatus("The selected splash could not be decoded from the installed WAD. Try another available skin.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Local splash selection failed: {ex}");
                SetSplashSearchStatus("Unable to load that game splash. The current background is unchanged.");
            }
            finally
            {
                _splashSearchBusy = false;
                SplashSearchButton.IsEnabled = true;
                SplashSearchBox.IsEnabled = true;
            }
        }

        private async Task<bool> ApplySplashAsync(string champion, int skinId)
        {
            string path = await _iconManager.FindSplashAsync(champion, skinId);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            AppSettings.Instance.CustomBackgroundPath = path;
            SplashResultsPanel.Visibility = Visibility.Collapsed;
            SetSplashSearchStatus($"Applied {champion} skin {skinId} from the local game files.");
            UpdatePreview();
            return true;
        }

        private void SetSplashSearchStatus(string message)
        {
            SplashSearchStatusText.Text = message;
            SplashSearchStatusText.Visibility = string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed : Visibility.Visible;
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
            RequestPreview(debounce: true);
        }

        private void UpdatePreview()
        {
            RequestPreview(debounce: false);
        }

        private void RequestPreview(bool debounce)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => RequestPreview(debounce)));
                return;
            }

            _previewCts?.Cancel();
            var cancellationSource = new CancellationTokenSource();
            _previewCts = cancellationSource;
            long generation = ++_previewGeneration;
            _ = GeneratePreviewAsync(generation, cancellationSource, debounce);
        }

        private async Task GeneratePreviewAsync(long generation, CancellationTokenSource cancellationSource, bool debounce)
        {
            CancellationToken token = cancellationSource.Token;
            try
            {
                if (debounce)
                    await Task.Delay(50, token);

                token.ThrowIfCancellationRequested();

                // Only the design editor uses this vector-derived sample icon.
                // Actual event images and their existing crop/border rules are untouched.
                string previewIconPath = null;
                try
                {
                    previewIconPath = PreviewIconArtwork.EnsureCreated(AppConfig.PreviewDialogueIconPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning($"Designer sample artwork unavailable: {ex.Message}");
                }

                var mockEvent = new ParsedEvent
                {
                    DisplayText = _model.PreviewText,
                    IconType = "champion",
                    IconLookupName = "Design Preview",
                    IconPath = previewIconPath,
                    Dialogue = "This is a dialogue preview. Adjust the bubble to suit your video."
                };

                string fontName = _model.SelectedFontName;
                string backgroundPath = AppSettings.Instance.CustomBackgroundPath;
                float verticalOffset = AppSettings.Instance.TextVerticalOffset;

                // Perform generation in background thread
                byte[] imageBytes = await Task.Run(() =>
                    _imageGenerator.CreateImageBytesAsync(mockEvent, fontName, backgroundPath, verticalOffset, token), token);

                if (imageBytes != null && !token.IsCancellationRequested && generation == _previewGeneration)
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
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested && generation == _previewGeneration)
                            _model.PreviewImage = bitmap;
                    });
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Background designer preview failed: {ex}");
            }
            finally
            {
                if (ReferenceEquals(_previewCts, cancellationSource))
                    _previewCts = null;
                cancellationSource.Dispose();
            }
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
            if (ModernMessageBox.Show("Are you sure you want to reset all visual layout parameters to their default values?", "Reset Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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
