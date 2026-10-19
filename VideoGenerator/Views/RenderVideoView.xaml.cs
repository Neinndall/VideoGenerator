using FFMpegCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views
{
    /// <summary>
    /// Non-destructive video assembly workspace. This UI edits a project, not the
    /// source files produced by the Dashboard. FFmpeg renders an independent MP4.
    /// </summary>
    public partial class RenderVideoView : UserControl
    {
        private readonly VideoService _videoService;
        private readonly VideoTimelineRenderService _renderer;
        private readonly LogService _logger;
        private readonly ProgressService _progress;
        private readonly TaskCancellationService _cancellation;
        private readonly DispatcherTimer _playbackTimer;
        private VideoTimelineProject _project = new() { Name = "Untitled montage" };
        private string _projectPath;
        private bool _dirty;
        private bool _initializingUi = true;
        private bool _playing;
        private bool _autoplayWhenOpened;
        private bool _updatingSlider;
        private bool _suppressSelectionOpen;
        private bool _exporting;
        private int _activeClipIndex = -1;
        private double _pendingClipSeek;
        private int _lastExportProgress;

        public ObservableCollection<MediaEntry> Library { get; } = new();
        public ObservableCollection<TimelineEntry> TimelineItems { get; } = new();

        public RenderVideoView(
            VideoService videoService,
            VideoTimelineRenderService renderer,
            LogService logger,
            ProgressService progress,
            TaskCancellationService cancellation)
        {
            _videoService = videoService;
            _renderer = renderer;
            _logger = logger;
            _progress = progress;
            _cancellation = cancellation;
            InitializeComponent();
            DataContext = this;
            QualityPresetBox.ItemsSource = TimelineExportPreset.Presets;
            QualityPresetBox.SelectedIndex = 0;
            MusicVolumeSlider.Value = _project.MusicVolume;
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            UpdateProjectUi();
            _initializingUi = false;
        }

        public sealed class MediaEntry
        {
            public string Path { get; init; }
            public string Name => System.IO.Path.GetFileName(Path);
            public double DurationSeconds { get; init; }
            public string TimeDisplay => FormatTime(DurationSeconds);
        }

        public sealed class TimelineEntry
        {
            public VideoTimelineClip Clip { get; init; }
            public string Name => string.IsNullOrWhiteSpace(Clip.DisplayName)
                ? System.IO.Path.GetFileNameWithoutExtension(Clip.SourcePath) : Clip.DisplayName;
            public string TimeDisplay => FormatTime(Clip.DurationSeconds);
            public double PixelWidth => Math.Clamp(Clip.DurationSeconds * 11d, 112d, 300d);
        }

        private static string FormatTime(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private double TotalDuration => _project.Clips.Sum(c => c.DurationSeconds);
        private string ResolveClipPath(VideoTimelineClip clip) => _project.ResolveMediaPath(clip.SourcePath);
        private void SetStatus(string message) => ProjectStatusText.Text =
            $"{_project.Name}{(_dirty ? " • Unsaved" : "")}   ·   {message}";

        private void MarkChanged(string message = "Timeline changed")
        {
            _dirty = true;
            RefreshTimeline();
            SetStatus(message);
        }

        private void RefreshTimeline(int? selectIndex = null)
        {
            int selected = selectIndex ?? TimelineList.SelectedIndex;
            TimelineItems.Clear();
            foreach (var clip in _project.Clips)
                TimelineItems.Add(new TimelineEntry { Clip = clip });
            if (selected >= 0 && selected < TimelineItems.Count)
                TimelineList.SelectedIndex = selected;
            TimelineDurationText.Text = $"  ·  {FormatTime(TotalDuration)}  /  {_project.Clips.Count} clips";
            MusicLaneText.Text = string.IsNullOrWhiteSpace(_project.MusicPath)
                ? "No background music · select an audio file in the inspector"
                : System.IO.Path.GetFileName(_project.MusicPath);
            UpdatePlayheadUi();
        }

        private void UpdateProjectUi()
        {
            RefreshTimeline();
            MusicFileText.Text = string.IsNullOrWhiteSpace(_project.MusicPath)
                ? "No music selected" : System.IO.Path.GetFileName(_project.MusicPath);
            MusicVolumeSlider.Value = Math.Clamp(_project.MusicVolume, 0, 1);
            SelectedClipText.Text = "Select a clip below";
            TrimStartBox.Text = "0";
            ClipDurationBox.Text = "0";
            SetStatus($"{_project.Clips.Count} clips · {FormatTime(TotalDuration)} total");
        }

        private async Task ImportPathsAsync(IEnumerable<string> inputPaths)
        {
            var paths = inputPaths.Where(File.Exists)
                .Select(System.IO.Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => !Library.Any(x => string.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (paths.Length == 0) return;
            try
            {
                SetStatus($"Inspecting {paths.Length} media files...");
                await _videoService.EnsureBinariesReadyAsync();
                int count = 0;
                var failed = new List<string>();
                foreach (string path in paths)
                {
                    try
                    {
                        var metadata = await FFProbe.AnalyseAsync(path);
                        if (metadata.Duration.TotalSeconds <= 0 ||
                            !double.IsFinite(metadata.Duration.TotalSeconds) ||
                            metadata.PrimaryVideoStream == null)
                            throw new InvalidDataException("No usable video stream or duration");
                        Library.Add(new MediaEntry { Path = path, DurationSeconds = metadata.Duration.TotalSeconds });
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn($"Timeline could not inspect '{path}': {ex.Message}");
                        failed.Add(System.IO.Path.GetFileName(path));
                    }
                }
                LibraryCountText.Text = $"{Library.Count} clips";
                SetStatus($"Imported {count} clips" + (failed.Count > 0 ? $" · {failed.Count} unavailable" : ""));
                if (failed.Count > 0)
                    MessageBox.Show("Could not open these media files:\n" +
                        string.Join("\n", failed.Take(10)), "Media import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ShowError("Could not prepare FFmpeg for media import.", ex);
            }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            var dialog = new OpenFileDialog
            {
                Title = "Import video clips",
                Filter = "Video files|*.mp4;*.mov;*.mkv;*.webm|All files|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
                await ImportPathsAsync(dialog.FileNames);
        }

        private async void ScanGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            if (!Directory.Exists(AppConfig.OutputVideosDir))
            {
                SetStatus("No generated videos yet. Render clips in Dashboard or use Import.");
                return;
            }
            try
            {
                string[] paths = await Task.Run(() =>
                    Directory.EnumerateFiles(AppConfig.OutputVideosDir, "*.mp4", SearchOption.AllDirectories).ToArray());
                await ImportPathsAsync(paths);
            }
            catch (Exception ex) { ShowError("Unable to scan Generated/Media.", ex); }
        }

        private void MediaLibrary_DoubleClick(object sender, MouseButtonEventArgs e) => AddSelected_Click(sender, e);

        private void AddSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting || MediaLibraryList.SelectedItem is not MediaEntry source) return;
            var clip = new VideoTimelineClip
            {
                SourcePath = source.Path,
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(source.Path),
                TrimStartSeconds = 0,
                DurationSeconds = source.DurationSeconds
            };
            int index = TimelineList.SelectedIndex;
            index = index < 0 ? _project.Clips.Count : index + 1;
            _project.Clips.Insert(index, clip);
            MarkChanged($"Added {clip.DisplayName}");
            TimelineList.SelectedIndex = index;
        }

        private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionOpen) return;
            if (TimelineList.SelectedItem is not TimelineEntry selected)
            {
                SelectedClipText.Text = "Select a clip below";
                return;
            }
            SelectedClipText.Text = selected.Name;
            TrimStartBox.Text = selected.Clip.TrimStartSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            ClipDurationBox.Text = selected.Clip.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            int index = TimelineList.SelectedIndex;
            if (!_exporting)
                OpenClip(index, false, selected.Clip.TrimStartSeconds);
        }

        private void SelectTimelineWithoutReload(int index)
        {
            _suppressSelectionOpen = true;
            try { TimelineList.SelectedIndex = index; }
            finally { _suppressSelectionOpen = false; }
            if (index >= 0 && index < _project.Clips.Count)
            {
                var selected = _project.Clips[index];
                SelectedClipText.Text = selected.DisplayName;
                TrimStartBox.Text = selected.TrimStartSeconds.ToString("0.###", CultureInfo.InvariantCulture);
                ClipDurationBox.Text = selected.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private void MoveClip(int direction)
        {
            if (_exporting) return;
            int from = TimelineList.SelectedIndex;
            int to = from + direction;
            if (from < 0 || to < 0 || to >= _project.Clips.Count) return;
            var clip = _project.Clips[from];
            _project.Clips.RemoveAt(from);
            _project.Clips.Insert(to, clip);
            MarkChanged("Timeline reordered");
            TimelineList.SelectedIndex = to;
        }
        private void MoveLeft_Click(object sender, RoutedEventArgs e) => MoveClip(-1);
        private void MoveRight_Click(object sender, RoutedEventArgs e) => MoveClip(1);

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            int index = TimelineList.SelectedIndex;
            if (index < 0 || index >= _project.Clips.Count) return;
            _project.Clips.RemoveAt(index);
            MarkChanged("Clip removed from timeline (source preserved)");
            if (_project.Clips.Count > 0)
                TimelineList.SelectedIndex = Math.Min(index, _project.Clips.Count - 1);
            else
                ClearMonitor();
        }

        private void TimelineList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_exporting || e.LeftButton != MouseButtonState.Pressed) return;
            if (TimelineList.SelectedItem is not TimelineEntry item) return;
            DragDrop.DoDragDrop(TimelineList, item.Clip, DragDropEffects.Move);
        }

        private void Timeline_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = !_exporting && e.Data.GetDataPresent(typeof(VideoTimelineClip))
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void Timeline_Drop(object sender, DragEventArgs e)
        {
            if (_exporting || e.Data.GetData(typeof(VideoTimelineClip)) is not VideoTimelineClip source) return;
            int from = _project.Clips.IndexOf(source);
            if (from < 0) return;
            int to = _project.Clips.Count - 1;
            var element = e.OriginalSource as DependencyObject;
            while (element != null)
            {
                if (element is ListBoxItem item && item.DataContext is TimelineEntry target)
                {
                    to = _project.Clips.IndexOf(target.Clip);
                    break;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            if (from == to || to < 0) return;
            _project.Clips.RemoveAt(from);
            _project.Clips.Insert(to, source);
            MarkChanged("Timeline reordered");
            TimelineList.SelectedIndex = to;
        }

        private void ApplyTrim_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting || TimelineList.SelectedItem is not TimelineEntry selected) return;
            if (!double.TryParse(TrimStartBox.Text.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double start) ||
                !double.TryParse(ClipDurationBox.Text.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double length) ||
                !double.IsFinite(start) || !double.IsFinite(length) || start < 0 || length <= 0)
            {
                MessageBox.Show("Enter a valid non-negative start and a duration greater than zero.",
                    "Invalid clip trim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var libraryDuration = Library.FirstOrDefault(x =>
                string.Equals(x.Path, ResolveClipPath(selected.Clip), StringComparison.OrdinalIgnoreCase))?.DurationSeconds;
            if (libraryDuration is double max && start + length > max + 0.02)
            {
                MessageBox.Show($"The selected clip has only {max:0.###} seconds of source video.",
                    "Trim exceeds source", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            selected.Clip.TrimStartSeconds = start;
            selected.Clip.DurationSeconds = length;
            MarkChanged("Trim updated");
            OpenClip(TimelineList.SelectedIndex, false, start);
        }

        private void OpenClip(int index, bool autoplay, double seekSeconds)
        {
            if (index < 0 || index >= _project.Clips.Count) { ClearMonitor(); return; }
            var clip = _project.Clips[index];
            string mediaPath = ResolveClipPath(clip);
            if (!File.Exists(mediaPath))
            {
                StopPlaying();
                SetStatus($"Missing source clip: {clip.DisplayName}");
                return;
            }
            _activeClipIndex = index;
            _autoplayWhenOpened = autoplay;
            _pendingClipSeek = Math.Clamp(seekSeconds, clip.TrimStartSeconds,
                clip.TrimStartSeconds + clip.DurationSeconds);
            _playing = false;
            _playbackTimer.Stop();
            VideoMonitor.Stop();
            VideoMonitor.Source = null;
            VideoMonitor.Source = new Uri(mediaPath, UriKind.Absolute);
            PreviewEmptyText.Visibility = Visibility.Collapsed;
            MonitorTitle.Text = clip.DisplayName;
            PlayPauseButton.Content = "PLAY";
            UpdatePlayheadUi();
        }

        private void VideoMonitor_MediaOpened(object sender, RoutedEventArgs e)
        {
            VideoMonitor.Position = TimeSpan.FromSeconds(_pendingClipSeek);
            if (_autoplayWhenOpened)
                StartPlaying();
            else
                VideoMonitor.Pause();
            UpdatePlayheadUi();
        }
        private void VideoMonitor_MediaEnded(object sender, RoutedEventArgs e) => AdvancePlayback();
        private void VideoMonitor_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            StopPlaying();
            SetStatus($"Playback unavailable: {e.ErrorException?.Message}");
        }

        private void ClearMonitor()
        {
            StopPlaying();
            _activeClipIndex = -1;
            VideoMonitor.Source = null;
            PreviewEmptyText.Visibility = Visibility.Visible;
            MonitorTitle.Text = "No clip selected";
            UpdatePlayheadUi();
        }
        private void StartPlaying()
        {
            if (_activeClipIndex < 0) return;
            VideoMonitor.Play();
            _playing = true;
            _playbackTimer.Start();
            PlayPauseButton.Content = "PAUSE";
        }
        private void StopPlaying()
        {
            _playing = false;
            _playbackTimer?.Stop();
            VideoMonitor?.Pause();
            if (PlayPauseButton != null) PlayPauseButton.Content = "PLAY";
        }
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_activeClipIndex < 0 && _project.Clips.Count > 0)
            {
                TimelineList.SelectedIndex = 0;
                OpenClip(0, true, _project.Clips[0].TrimStartSeconds);
                return;
            }
            if (_playing) StopPlaying();
            else if (_activeClipIndex >= 0) StartPlaying();
        }
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_activeClipIndex < 0 || _activeClipIndex >= _project.Clips.Count) return;
            var clip = _project.Clips[_activeClipIndex];
            if (VideoMonitor.Position.TotalSeconds >= clip.TrimStartSeconds + clip.DurationSeconds - 0.08)
                AdvancePlayback();
            else UpdatePlayheadUi();
        }
        private void AdvancePlayback()
        {
            int next = _activeClipIndex + 1;
            if (next >= _project.Clips.Count)
            {
                StopPlaying();
                UpdatePlayheadUi();
            }
            else
            {
                OpenClip(next, true, _project.Clips[next].TrimStartSeconds);
                SelectTimelineWithoutReload(next);
            }
        }
        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_project.Clips.Count == 0) return;
            int index = Math.Max(0, _activeClipIndex - 1);
            OpenClip(index, _playing, _project.Clips[index].TrimStartSeconds);
            SelectTimelineWithoutReload(index);
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_project.Clips.Count == 0) return;
            int index = Math.Clamp(_activeClipIndex + 1, 0, _project.Clips.Count - 1);
            OpenClip(index, _playing, _project.Clips[index].TrimStartSeconds);
            SelectTimelineWithoutReload(index);
        }
        private void UpdatePlayheadUi()
        {
            if (PlayheadSlider == null) return;
            double position = 0;
            if (_activeClipIndex >= 0 && _activeClipIndex < _project.Clips.Count)
            {
                position = _project.Clips.Take(_activeClipIndex).Sum(c => c.DurationSeconds);
                var clip = _project.Clips[_activeClipIndex];
                position += Math.Clamp(VideoMonitor.Position.TotalSeconds - clip.TrimStartSeconds, 0, clip.DurationSeconds);
            }
            _updatingSlider = true;
            PlayheadSlider.Maximum = Math.Max(1, TotalDuration);
            PlayheadSlider.Value = Math.Clamp(position, 0, PlayheadSlider.Maximum);
            _updatingSlider = false;
            MonitorTimeText.Text = $"{FormatTime(position)} / {FormatTime(TotalDuration)}";
        }
        private void PlayheadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingSlider || _project == null || _project.Clips.Count == 0) return;
            double target = Math.Clamp(e.NewValue, 0, TotalDuration);
            double elapsed = 0;
            for (int i = 0; i < _project.Clips.Count; i++)
            {
                var clip = _project.Clips[i];
                if (target <= elapsed + clip.DurationSeconds || i == _project.Clips.Count - 1)
                {
                    double seek = clip.TrimStartSeconds + Math.Clamp(target - elapsed, 0, clip.DurationSeconds);
                    if (_activeClipIndex == i && VideoMonitor.Source != null)
                        VideoMonitor.Position = TimeSpan.FromSeconds(seek);
                    else
                    {
                        OpenClip(i, _playing, seek);
                        SelectTimelineWithoutReload(i);
                    }
                    break;
                }
                elapsed += clip.DurationSeconds;
            }
            UpdatePlayheadUi();
        }

        private void ChooseMusic_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            var dialog = new OpenFileDialog
            {
                Filter = "Audio files|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac|All files|*.*",
                Title = "Choose background music"
            };
            if (dialog.ShowDialog() != true) return;
            _project.MusicPath = System.IO.Path.GetFullPath(dialog.FileName);
            MusicFileText.Text = System.IO.Path.GetFileName(_project.MusicPath);
            MarkChanged("Background music selected");
        }
        private void ClearMusic_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            _project.MusicPath = null;
            MusicFileText.Text = "No music selected";
            MarkChanged("Background music removed");
        }
        private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_project == null || _exporting || _initializingUi) return;
            if (Math.Abs(_project.MusicVolume - e.NewValue) > 0.001)
            {
                _project.MusicVolume = e.NewValue;
                _dirty = true;
                SetStatus("Music volume changed");
            }
        }
        private void QualityPresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityPresetBox?.SelectedItem is TimelineExportPreset preset)
                PresetDetailText.Text = $"{preset.Width} × {preset.Height} · {preset.Fps} fps · H.264 CRF {preset.Crf} · AAC";
        }
        private bool ConfirmDiscard()
        {
            return !_dirty || MessageBox.Show("Discard unsaved changes to this timeline project?",
                "Unsaved montage", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting || !ConfirmDiscard()) return;
            ClearMonitor();
            _project = new VideoTimelineProject { Name = "Untitled montage" };
            _projectPath = null;
            _dirty = false;
            UpdateProjectUi();
        }
        private async void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting || !ConfirmDiscard()) return;
            var dialog = new OpenFileDialog
            {
                Title = "Open VideoGenerator montage",
                Filter = "VideoGenerator montage|*.vgproject|JSON project|*.json"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var loaded = await VideoTimelineProject.LoadAsync(dialog.FileName);
                ClearMonitor();
                _project = loaded;
                _projectPath = dialog.FileName;
                _dirty = false;
                UpdateProjectUi();
                await ImportPathsAsync(_project.Clips.Select(ResolveClipPath));
                var missing = _project.Clips.Where(c => !File.Exists(ResolveClipPath(c))).ToArray();
                if (missing.Length > 0)
                    SetStatus($"{missing.Length} missing source clip(s) · relink files before export");
                RefreshTimeline();
            }
            catch (Exception ex) { ShowError("Could not open the montage project.", ex); }
        }
        private async void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting) return;
            var path = _projectPath;
            if (string.IsNullOrEmpty(path))
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save montage project",
                    Filter = "VideoGenerator montage|*.vgproject",
                    FileName = "VideoGenerator_Montage.vgproject"
                };
                if (dialog.ShowDialog() != true) return;
                path = dialog.FileName;
            }
            try
            {
                _project.Name = System.IO.Path.GetFileNameWithoutExtension(path);
                await _project.SaveAsync(path);
                _projectPath = path;
                _dirty = false;
                SetStatus("Project saved · original videos unchanged");
            }
            catch (Exception ex) { ShowError("Could not save montage project.", ex); }
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_exporting || _project.Clips.Count == 0)
            {
                if (!_exporting) SetStatus("Add videos to the timeline before exporting");
                return;
            }
            if (QualityPresetBox.SelectedItem is not TimelineExportPreset preset) return;
            var dialog = new SaveFileDialog
            {
                Title = "Render complete montage to MP4",
                Filter = "MP4 video|*.mp4",
                FileName = "Montage.mp4",
                InitialDirectory = Directory.Exists(AppConfig.OutputVideosDir)
                    ? AppConfig.OutputVideosDir : null
            };
            if (dialog.ShowDialog() != true) return;
            if (_project.Clips.Any(c => string.Equals(ResolveClipPath(c),
                    System.IO.Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("The export file cannot overwrite a source clip.", "Unsafe export",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _exporting = true;
            _lastExportProgress = 0;
            ExportButton.IsEnabled = false;
            var token = _cancellation.CreateNewToken();
            _progress.StartWork("Rendering timeline montage", 100);
            // Freeze the timeline snapshot so UI edits cannot change an in-flight render.
            var snapshot = new VideoTimelineProject
            {
                Name = _project.Name,
                MusicPath = string.IsNullOrWhiteSpace(_project.MusicPath)
                    ? null : _project.ResolveMediaPath(_project.MusicPath),
                MusicVolume = _project.MusicVolume,
                Clips = _project.Clips.Select(c => new VideoTimelineClip
                {
                    SourcePath = ResolveClipPath(c),
                    DisplayName = c.DisplayName,
                    TrimStartSeconds = c.TrimStartSeconds,
                    DurationSeconds = c.DurationSeconds
                }).ToList()
            };
            try
            {
                SetStatus("Export running · Esc or global Cancel to stop");
                var report = new Progress<double>(value =>
                {
                    int percent = Math.Clamp((int)Math.Round(value * 100), 0, 100);
                    if (percent > _lastExportProgress)
                    {
                        _progress.Advance(percent - _lastExportProgress,
                            $"Rendering montage: {percent}%");
                        _lastExportProgress = percent;
                    }
                });
                await _renderer.ExportAsync(snapshot, dialog.FileName, preset, report, token);
                _progress.FinishWork("Montage exported");
                SetStatus($"Export complete · {System.IO.Path.GetFileName(dialog.FileName)}");
                MessageBox.Show($"Montage successfully exported:\n{dialog.FileName}",
                    "Render Video", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Export canceled · original clips preserved");
            }
            catch (Exception ex) { ShowError("Timeline export failed; source clips were not changed.", ex); }
            finally
            {
                _exporting = false;
                ExportButton.IsEnabled = true;
                await _progress.CompleteAsync();
            }
        }
        private void ShowError(string heading, Exception ex)
        {
            _logger.LogError(heading, ex);
            SetStatus(heading);
            MessageBox.Show($"{heading}\n{ex.Message}", "Render Video",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void Editor_Unloaded(object sender, RoutedEventArgs e)
        {
            StopPlaying();
            if (VideoMonitor != null) VideoMonitor.Close();
        }
        private void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (_activeClipIndex >= 0 && _activeClipIndex < _project.Clips.Count &&
                VideoMonitor.Source == null)
                OpenClip(_activeClipIndex, false, _project.Clips[_activeClipIndex].TrimStartSeconds);
        }
    }
}
