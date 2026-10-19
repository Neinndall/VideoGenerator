#nullable enable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using MessageBox = System.Windows.MessageBox;

namespace VideoGenerator.Views.Dialogs;

public partial class RenderExportWindow : Window
{
    private readonly VideoTimelineProject _project;
    private readonly VideoTimelineRenderService _renderer;
    private readonly RenderPresetManager _presetManager;
    private readonly LogService _logger;
    private readonly ProgressService _progress;
    private readonly TaskCancellationService _cancellation;

    private List<TimelineExportPreset> _allPresets = new();
    private TimelineExportPreset? _selectedPreset;
    private bool _loadingPreset;
    private bool _isRendering;
    private CancellationTokenSource? _activeRenderCts;
    private string? _renderedFilePath;

    public RenderExportWindow(
        VideoTimelineProject project,
        VideoTimelineRenderService renderer,
        RenderPresetManager presetManager,
        LogService logger,
        ProgressService progress,
        TaskCancellationService cancellation)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _presetManager = presetManager ?? throw new ArgumentNullException(nameof(presetManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));

        InitializeComponent();
        InitializeStandardResolutions();
        LoadProjectSummary();
        InitializeDefaultDestination();
        Loaded += RenderExportWindow_Loaded;
    }

    private async void RenderExportWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadPresetsAsync();
    }

    private void LoadProjectSummary()
    {
        double totalSeconds = _project.Clips.Sum(c => c.DurationSeconds);
        var timeSpan = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        string formattedDuration = timeSpan.TotalHours >= 1
            ? timeSpan.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : timeSpan.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

        ProjectSummaryText.Text = $"{_project.Name} · {_project.Clips.Count} clip(s) · {formattedDuration} total";
    }

    private void InitializeDefaultDestination()
    {
        string baseDir = Directory.Exists(AppConfig.OutputVideosDir)
            ? AppConfig.OutputVideosDir
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        string safeName = string.Join("_", _project.Name.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Montage";

        DestinationPathBox.Text = Path.Combine(baseDir, $"{safeName}.mp4");
    }

    private void InitializeStandardResolutions()
    {
        StandardResComboBox.Items.Clear();
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "1080p Full HD (1920 × 1080 · 16:9)", Tag = (1920, 1080) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "720p HD (1280 × 720 · 16:9)", Tag = (1280, 720) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "TikTok / Shorts / Reels (1080 × 1920 · 9:16)", Tag = (1080, 1920) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "1440p 2K QHD (2560 × 1440 · 16:9)", Tag = (2560, 1440) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "4K Ultra HD (3840 × 2160 · 16:9)", Tag = (3840, 2160) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "Square (1080 × 1080 · 1:1)", Tag = (1080, 1080) });
        StandardResComboBox.Items.Add(new ComboBoxItem { Content = "Custom Resolution...", Tag = null });
    }

    private async Task ReloadPresetsAsync(string? selectPresetName = null)
    {
        await _presetManager.InitializeAsync();
        _allPresets = _presetManager.GetAllPresets().ToList();
        ApplyFilterAndSearch(selectPresetName);
    }

    private void ApplyFilterAndSearch(string? selectPresetName = null)
    {
        string query = SearchBox.Text?.Trim() ?? "";
        bool filterBuiltIn = FilterBuiltInRadio.IsChecked == true;
        bool filterCustom = FilterCustomRadio.IsChecked == true;

        var filtered = _allPresets.Where(p =>
        {
            if (filterBuiltIn && !p.IsBuiltIn) return false;
            if (filterCustom && p.IsBuiltIn) return false;
            if (!string.IsNullOrWhiteSpace(query))
            {
                bool nameMatch = p.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool descMatch = p.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                return nameMatch || descMatch;
            }
            return true;
        }).ToList();

        PresetsListBox.ItemsSource = filtered;

        // Select specific or first preset
        if (!string.IsNullOrEmpty(selectPresetName))
        {
            var match = filtered.FirstOrDefault(p => string.Equals(p.Name, selectPresetName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                PresetsListBox.SelectedItem = match;
                return;
            }
        }

        if (PresetsListBox.SelectedItem is null && filtered.Count > 0)
        {
            PresetsListBox.SelectedIndex = 0;
        }
    }

    private void PresetsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetsListBox.SelectedItem is not TimelineExportPreset preset)
            return;

        _selectedPreset = preset;
        PopulateCustomizer(preset);
    }

    private void PopulateCustomizer(TimelineExportPreset preset)
    {
        _loadingPreset = true;
        try
        {
            PresetNameBox.Text = preset.Name;
            PresetDescBox.Text = preset.Description;
            WidthBox.Text = preset.Width.ToString();
            HeightBox.Text = preset.Height.ToString();

            // Match standard resolution
            bool matchedRes = false;
            for (int i = 0; i < StandardResComboBox.Items.Count - 1; i++)
            {
                if (StandardResComboBox.Items[i] is ComboBoxItem item && item.Tag is ValueTuple<int, int> dim)
                {
                    if (dim.Item1 == preset.Width && dim.Item2 == preset.Height)
                    {
                        StandardResComboBox.SelectedIndex = i;
                        matchedRes = true;
                        break;
                    }
                }
            }
            if (!matchedRes)
            {
                StandardResComboBox.SelectedIndex = StandardResComboBox.Items.Count - 1; // Custom
            }

            // Match FPS
            for (int i = 0; i < FpsComboBox.Items.Count; i++)
            {
                if (FpsComboBox.Items[i] is ComboBoxItem item && item.Content.ToString()!.Contains($"{preset.Fps} FPS"))
                {
                    FpsComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Match Encoder
            for (int i = 0; i < EncoderComboBox.Items.Count; i++)
            {
                if (EncoderComboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), preset.Encoder, StringComparison.OrdinalIgnoreCase))
                {
                    EncoderComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (EncoderComboBox.SelectedIndex < 0) EncoderComboBox.SelectedIndex = 0;

            // Match CRF
            CrfSlider.Value = Math.Clamp(preset.Crf, CrfSlider.Minimum, CrfSlider.Maximum);
            UpdateCrfDisplay((int)CrfSlider.Value);

            // Match Audio Bitrate
            for (int i = 0; i < AudioBitrateComboBox.Items.Count; i++)
            {
                if (AudioBitrateComboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), preset.AudioBitrate, StringComparison.OrdinalIgnoreCase))
                {
                    AudioBitrateComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (AudioBitrateComboBox.SelectedIndex < 0) AudioBitrateComboBox.SelectedIndex = 1; // 192k default

            // Match Audio Sample Rate
            for (int i = 0; i < AudioSampleRateComboBox.Items.Count; i++)
            {
                if (AudioSampleRateComboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), preset.AudioSampleRate.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    AudioSampleRateComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (AudioSampleRateComboBox.SelectedIndex < 0) AudioSampleRateComboBox.SelectedIndex = 0; // 48000 default

            // UI badge & buttons
            TemplateBadgeText.Text = preset.IsBuiltIn ? "BUILT-IN TEMPLATE" : "CUSTOM TEMPLATE";
            TemplateBadgeText.Foreground = (System.Windows.Media.Brush)FindResource(
                preset.IsBuiltIn ? "TextMutedBrush" : "AccentBrushLight");

            DeletePresetButton.IsEnabled = !preset.IsBuiltIn;
            SaveTemplateButton.Content = preset.IsBuiltIn ? "CLONE & SAVE AS CUSTOM" : "SAVE TEMPLATE CHANGES";

            UpdateEstimatedSize();
        }
        finally
        {
            _loadingPreset = false;
        }
    }

    private void StandardResComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreset) return;
        if (StandardResComboBox.SelectedItem is ComboBoxItem item && item.Tag is ValueTuple<int, int> dim)
        {
            WidthBox.Text = dim.Item1.ToString();
            HeightBox.Text = dim.Item2.ToString();
            UpdateEstimatedSize();
        }
    }

    private void CrfSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int crf = (int)Math.Round(e.NewValue);
        UpdateCrfDisplay(crf);
        UpdateEstimatedSize();
    }

    private void UpdateCrfDisplay(int crf)
    {
        if (CrfValueText == null) return;
        string qualityDesc = crf switch
        {
            <= 17 => "Ultra High Quality (Visually Lossless)",
            <= 20 => "High Quality (Recommended)",
            <= 23 => "Balanced (Standard)",
            <= 26 => "Medium (Smaller File)",
            _ => "Low Bitrate (Maximum Compression)"
        };
        CrfValueText.Text = $"CRF {crf} · {qualityDesc}";
    }

    private void CustomSetting_Changed(object sender, EventArgs e)
    {
        if (_loadingPreset) return;
        UpdateEstimatedSize();
    }

    private void UpdateEstimatedSize()
    {
        if (EstimatedSizeText == null) return;

        double totalSeconds = _project.Clips.Sum(c => c.DurationSeconds);
        if (totalSeconds <= 0)
        {
            EstimatedSizeText.Text = "Estimated size: ~0 MB (no clips in timeline)";
            return;
        }

        int crf = (int)Math.Round(CrfSlider?.Value ?? 21);
        int width = int.TryParse(WidthBox?.Text, out int w) ? w : 1920;
        int height = int.TryParse(HeightBox?.Text, out int h) ? h : 1080;
        long pixelArea = (long)width * height;

        // Empirical bitrate estimation by CRF and resolution
        double baseBitrateMbps = crf switch
        {
            <= 17 => 16.0,
            <= 19 => 11.0,
            <= 21 => 8.0,
            <= 24 => 5.0,
            <= 27 => 3.2,
            _ => 2.0
        };

        // Scale by resolution relative to 1080p (2.07M pixels)
        double resolutionRatio = Math.Clamp((double)pixelArea / (1920.0 * 1080.0), 0.35, 4.0);
        double videoBitrateMbps = baseBitrateMbps * Math.Sqrt(resolutionRatio);
        double audioBitrateMbps = 0.192; // 192k audio

        double totalBitrateMbps = videoBitrateMbps + audioBitrateMbps;
        double estimatedBytes = (totalBitrateMbps * 1_000_000.0 / 8.0) * totalSeconds;
        double estimatedMb = estimatedBytes / (1024.0 * 1024.0);

        EstimatedSizeText.Text = estimatedMb >= 1024
            ? $"Estimated size: ~{estimatedMb / 1024.0:F1} GB ({videoBitrateMbps:F1} Mbps video)"
            : $"Estimated size: ~{estimatedMb:F0} MB ({videoBitrateMbps:F1} Mbps video)";
    }

    private TimelineExportPreset BuildCurrentPresetFromUi(bool forceCustom = false)
    {
        string name = PresetNameBox.Text?.Trim() ?? "Custom Template";
        string desc = PresetDescBox.Text?.Trim() ?? "";

        int width = int.TryParse(WidthBox.Text, out int w) ? w : 1920;
        int height = int.TryParse(HeightBox.Text, out int h) ? h : 1080;
        // Ensure even dimensions for H.264
        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++;

        int fps = 30;
        if (FpsComboBox.SelectedItem is ComboBoxItem fpsItem)
        {
            string content = fpsItem.Content.ToString() ?? "";
            if (content.Contains("60")) fps = 60;
            else if (content.Contains("24")) fps = 24;
            else if (content.Contains("25")) fps = 25;
            else fps = 30;
        }

        string encoder = "libx264";
        if (EncoderComboBox.SelectedItem is ComboBoxItem encItem && encItem.Tag is string enc)
            encoder = enc;

        int crf = (int)Math.Round(CrfSlider.Value);

        string audioBitrate = "192k";
        if (AudioBitrateComboBox.SelectedItem is ComboBoxItem abItem && abItem.Tag is string ab)
            audioBitrate = ab;

        int audioSampleRate = 48000;
        if (AudioSampleRateComboBox.SelectedItem is ComboBoxItem asrItem && int.TryParse(asrItem.Tag?.ToString(), out int asr))
            audioSampleRate = asr;

        bool isBuiltIn = !forceCustom && (_selectedPreset?.IsBuiltIn == true);

        return new TimelineExportPreset(
            Name: name,
            Width: width,
            Height: height,
            Fps: fps,
            Crf: crf,
            AudioBitrate: audioBitrate,
            AudioSampleRate: audioSampleRate,
            Encoder: encoder,
            FfmpegPreset: "medium",
            IsBuiltIn: isBuiltIn,
            Description: desc
        );
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var preset = BuildCurrentPresetFromUi(forceCustom: true);
            var saved = await _presetManager.SavePresetAsync(preset);
            await ReloadPresetsAsync(saved.Name);
            RenderStatusText.Text = $"Template '{saved.Name}' saved successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save template: {ex.Message}", "Save Template", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SaveAsNewTemplate_Click(object sender, RoutedEventArgs e)
    {
        string currentName = PresetNameBox.Text?.Trim() ?? "New Template";
        string newName = currentName.StartsWith("Custom ", StringComparison.OrdinalIgnoreCase)
            ? $"{currentName} (Copy)"
            : $"Custom {currentName}";

        PresetNameBox.Text = newName;
        try
        {
            var preset = BuildCurrentPresetFromUi(forceCustom: true) with { Name = newName };
            var saved = await _presetManager.SavePresetAsync(preset);
            await ReloadPresetsAsync(saved.Name);
            RenderStatusText.Text = $"Created new custom template '{saved.Name}'.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create new template: {ex.Message}", "Save As", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void NewCustomPreset_Click(object sender, RoutedEventArgs e)
    {
        string uniqueName = $"Custom Template {_presetManager.GetCustomPresets().Count + 1}";
        var fresh = new TimelineExportPreset(
            Name: uniqueName,
            Width: 1920,
            Height: 1080,
            Fps: 60,
            Crf: 20,
            AudioBitrate: "192k",
            AudioSampleRate: 48000,
            Encoder: "libx264",
            FfmpegPreset: "medium",
            IsBuiltIn: false,
            Description: "Custom user render preset"
        );

        try
        {
            var saved = await _presetManager.SavePresetAsync(fresh);
            await ReloadPresetsAsync(saved.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create template: {ex.Message}", "New Template", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null || _selectedPreset.IsBuiltIn) return;

        if (MessageBox.Show($"Are you sure you want to delete custom template '{_selectedPreset.Name}'?",
            "Delete Template", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        string nameToDelete = _selectedPreset.Name;
        bool deleted = await _presetManager.DeleteCustomPresetAsync(nameToDelete);
        if (deleted)
        {
            await ReloadPresetsAsync();
            RenderStatusText.Text = $"Deleted template '{nameToDelete}'.";
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilterAndSearch();
    private void FilterRadio_Checked(object sender, RoutedEventArgs e) => ApplyFilterAndSearch();

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Rendered Video",
            Filter = "MP4 Video|*.mp4|All files|*.*",
            FileName = Path.GetFileName(DestinationPathBox.Text),
            InitialDirectory = Path.GetDirectoryName(DestinationPathBox.Text)
        };
        if (dialog.ShowDialog(this) == true)
        {
            DestinationPathBox.Text = dialog.FileName;
        }
    }

    private async void StartRender_Click(object sender, RoutedEventArgs e)
    {
        if (_isRendering) return;

        if (_project.Clips.Count == 0)
        {
            MessageBox.Show("There are no clips in the timeline to render. Add at least one clip.",
                "Render Video", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string outputPath = DestinationPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(outputPath) || !string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Please choose a valid destination .mp4 file path.",
                "Render Video", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var preset = BuildCurrentPresetFromUi();
        var validationErrors = preset.Validate();
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, validationErrors),
                "Invalid Preset", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Freeze project snapshot for background render
        var snapshot = new VideoTimelineProject
        {
            Name = _project.Name,
            MusicPath = string.IsNullOrWhiteSpace(_project.MusicPath)
                ? null : _project.ResolveMediaPath(_project.MusicPath),
            MusicVolume = _project.MusicVolume,
            Clips = _project.Clips.Select(c => new VideoTimelineClip
            {
                SourcePath = _project.ResolveMediaPath(c.SourcePath),
                DisplayName = c.DisplayName,
                TrimStartSeconds = c.TrimStartSeconds,
                DurationSeconds = c.DurationSeconds
            }).ToList()
        };

        _isRendering = true;
        _activeRenderCts = new CancellationTokenSource();
        StartRenderButton.IsEnabled = false;
        CancelButton.Content = "CANCEL RENDER";
        RenderProgressBar.Visibility = Visibility.Visible;
        RenderProgressBar.Value = 0;
        RenderStatusText.Text = "Initializing FFmpeg render pipeline...";
        OpenFolderButton.Visibility = Visibility.Collapsed;

        var stopwatch = Stopwatch.StartNew();
        var progressReporter = new Progress<double>(val =>
        {
            int pct = Math.Clamp((int)Math.Round(val * 100), 0, 100);
            RenderProgressBar.Value = pct;
            RenderStatusText.Text = $"Rendering: {pct}% · Elapsed: {stopwatch.Elapsed:mm\\:ss}";
        });

        try
        {
            await _renderer.ExportAsync(snapshot, outputPath, preset, progressReporter, _activeRenderCts.Token);
            _renderedFilePath = outputPath;
            RenderProgressBar.Value = 100;
            RenderStatusText.Text = $"Render completed in {stopwatch.Elapsed:mm\\:ss}! File saved: {Path.GetFileName(outputPath)}";
            OpenFolderButton.Visibility = Visibility.Visible;
            CancelButton.Content = "CLOSE";
            _logger.LogInfo($"Montage rendered successfully: {outputPath}");

            MessageBox.Show($"Render complete!\n\nVideo saved to:\n{outputPath}",
                "Render As... — VideoGenerator", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            RenderStatusText.Text = "Render canceled by user. Incomplete output deleted.";
            CancelButton.Content = "CLOSE";
        }
        catch (Exception ex)
        {
            RenderStatusText.Text = "Render error occurred.";
            _logger.LogError("Render export failed.", ex);
            MessageBox.Show($"Render failed:\n{ex.Message}", "Render Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CancelButton.Content = "CLOSE";
        }
        finally
        {
            _isRendering = false;
            StartRenderButton.IsEnabled = true;
            _activeRenderCts?.Dispose();
            _activeRenderCts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            if (_activeRenderCts != null && !_activeRenderCts.IsCancellationRequested)
            {
                RenderStatusText.Text = "Stopping render process...";
                _activeRenderCts.Cancel();
            }
        }
        else
        {
            Close();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_renderedFilePath) && File.Exists(_renderedFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_renderedFilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Could not open destination folder in Explorer.", ex);
            }
        }
    }
}
