#nullable enable
using FFMpegCore;
using MaterialDesignThemes.Wpf;
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
using VideoGenerator.Views.Dialogs;
using VideoGenerator.Views.Models;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views;

/// <summary>
/// Non-destructive video assembly workspace. This UI edits a montage project,
/// preserving source files. FFmpeg renders independent MP4s via the Render As dialog.
/// </summary>
public partial class RenderVideoView : UserControl
{
    private readonly VideoService _videoService;
    private readonly VideoTimelineRenderService _renderer;
    private readonly LogService _logger;
    private readonly ProgressService _progress;
    private readonly TaskCancellationService _cancellation;
    private readonly RenderPresetManager _presetManager;
    private readonly DispatcherTimer _playbackTimer;

    private VideoTimelineProject _project = new() { Name = "Untitled montage" };
    private string? _projectPath;
    private bool _dirty;
    private bool _initializingUi = true;
    private bool _playing;
    private bool _autoplayWhenOpened;
    private bool _updatingSlider;
    private bool _suppressSelectionOpen;
    private bool _exporting;
    private int _activeClipIndex = -1;
    private double _pendingClipSeek;

    private readonly List<MediaEntry> _allLibraryEntries = new();
    public ObservableCollection<MediaEntry> Library { get; } = new();
    public ObservableCollection<TimelineEntry> TimelineItems { get; } = new();

    public RenderVideoView(
        VideoService videoService,
        VideoTimelineRenderService renderer,
        LogService logger,
        ProgressService progress,
        TaskCancellationService cancellation,
        RenderPresetManager presetManager)
    {
        _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        _presetManager = presetManager ?? throw new ArgumentNullException(nameof(presetManager));

        InitializeComponent();
        DataContext = this;

        _initializingUi = false;
        if (MusicVolumeSlider != null)
            MusicVolumeSlider.Value = _project.MusicVolume;

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        RefreshTimeline();
        UpdateProjectUi();
    }

    public sealed class MediaEntry
    {
        public string Path { get; init; } = "";
        public string Name => System.IO.Path.GetFileName(Path);
        public double DurationSeconds { get; init; }
        public string TimeDisplay => FormatTime(DurationSeconds);
    }

    public sealed class TimelineEntry
    {
        public static double CurrentZoomFactor { get; set; } = 11d;

        public VideoTimelineClip Clip { get; init; } = null!;
        public string Name => string.IsNullOrWhiteSpace(Clip.DisplayName)
            ? System.IO.Path.GetFileNameWithoutExtension(Clip.SourcePath) : Clip.DisplayName;
        public string TimeDisplay => FormatTime(Clip.DurationSeconds);
        public double PixelWidth => Math.Clamp(Clip.DurationSeconds * CurrentZoomFactor, 85d, 700d);
    }

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private string ResolveClipPath(VideoTimelineClip clip) => _project.ResolveMediaPath(clip.SourcePath);

    private void SetStatus(string message)
    {
        if (ProjectStatusText != null) ProjectStatusText.Text = message;
    }

    private void MarkChanged(string statusMessage)
    {
        _dirty = true;
        UpdateProjectUi();
        SetStatus(statusMessage);
    }

    private void UpdateProjectUi()
    {
        if (_initializingUi) return;
        double totalSeconds = _project.Clips.Sum(c => c.DurationSeconds);
        string formattedDuration = FormatTime(totalSeconds);
        string dirtyIndicator = _dirty ? "*" : "";

        if (TimelineDurationText != null)
            TimelineDurationText.Text = $"  ·  {formattedDuration}  ({_project.Clips.Count} clips)";

        string pathDisplay = string.IsNullOrEmpty(_projectPath)
            ? _project.Name
            : System.IO.Path.GetFileName(_projectPath);

        SetStatus($"{dirtyIndicator}{pathDisplay} · {_project.Clips.Count} clip(s) · {formattedDuration} total");

        if (MusicFileText != null)
        {
            MusicFileText.Text = string.IsNullOrWhiteSpace(_project.MusicPath)
                ? "No music selected"
                : System.IO.Path.GetFileName(_project.MusicPath);
        }
        if (MusicLaneText != null)
        {
            MusicLaneText.Text = string.IsNullOrWhiteSpace(_project.MusicPath)
                ? "No background music · Select an audio file in the inspector"
                : $"Music: {System.IO.Path.GetFileName(_project.MusicPath)} (Volume: {(int)Math.Round(_project.MusicVolume * 100)}%)";
        }
    }

    public void RefreshTimeline()
    {
        if (_initializingUi || TimelineList == null) return;
        int prevSelected = TimelineList.SelectedIndex;
        TimelineItems.Clear();
        foreach (var clip in _project.Clips)
        {
            TimelineItems.Add(new TimelineEntry { Clip = clip });
        }
        UpdateProjectUi();
        if (prevSelected >= 0 && prevSelected < TimelineItems.Count)
            TimelineList.SelectedIndex = prevSelected;
    }

    #region Media Library Operations

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Import video clips",
            Filter = "Video files|*.mp4;*.webm;*.mov;*.mkv;*.avi|All files|*.*"
        };
        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0) return;
        await ImportPathsAsync(dialog.FileNames);
    }

    private async void ScanGenerated_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        string dir = AppConfig.OutputVideosDir;
        if (!Directory.Exists(dir))
        {
            SetStatus("No generated media directory found yet");
            return;
        }
        var files = Directory.GetFiles(dir, "*.mp4", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            SetStatus("No MP4 files found in Generated/Media");
            return;
        }
        await ImportPathsAsync(files);
    }

    private async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        var list = paths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).Distinct().ToList();
        if (list.Count == 0) return;

        int added = 0;
        foreach (var file in list)
        {
            string full = System.IO.Path.GetFullPath(file);
            if (_allLibraryEntries.Any(x => string.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                var info = await _renderer.InspectClipAsync(full);
                if (!info.HasVideo) continue;

                var entry = new MediaEntry { Path = full, DurationSeconds = info.DurationSeconds };
                _allLibraryEntries.Add(entry);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogWarn($"Skipped unreadable media: {full} ({ex.Message})");
            }
        }

        FilterLibrary();
        if (added > 0)
            SetStatus($"Imported {added} clip(s) into Media Library");
    }

    private void SearchLibraryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterLibrary();
    }

    private void FilterLibrary()
    {
        if (_initializingUi) return;
        string query = SearchLibraryBox?.Text?.Trim() ?? "";
        Library.Clear();
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allLibraryEntries
            : _allLibraryEntries.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in matches)
            Library.Add(entry);

        if (LibraryCountText != null)
            LibraryCountText.Text = $"{Library.Count} clip(s)";
    }

    private void ClearLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        _allLibraryEntries.Clear();
        Library.Clear();
        if (LibraryCountText != null) LibraryCountText.Text = "0 clips";
        SetStatus("Media library cleared");
    }

    private void MediaLibrary_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_exporting || MediaLibraryList.SelectedItem is not MediaEntry entry) return;
        AddClipToTimelineInternal(entry);
    }

    private void AddClipToTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        if (sender is Button btn && btn.Tag is MediaEntry entry)
        {
            AddClipToTimelineInternal(entry);
        }
    }

    private void AddAllToTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || Library.Count == 0) return;
        foreach (var entry in Library)
        {
            _project.Clips.Add(new VideoTimelineClip
            {
                SourcePath = entry.Path,
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(entry.Path),
                TrimStartSeconds = 0,
                DurationSeconds = entry.DurationSeconds
            });
        }
        MarkChanged($"Added {Library.Count} clip(s) to timeline");
        RefreshTimeline();
    }

    private void AddClipToTimelineInternal(MediaEntry entry)
    {
        _project.Clips.Add(new VideoTimelineClip
        {
            SourcePath = entry.Path,
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(entry.Path),
            TrimStartSeconds = 0,
            DurationSeconds = entry.DurationSeconds
        });
        MarkChanged($"Added {entry.Name} to timeline");
        RefreshTimeline();
        TimelineList.SelectedIndex = _project.Clips.Count - 1;
    }

    #endregion

    #region Timeline Operations (Split, In/Out, Duplicate, Reorder)

    private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionOpen || _initializingUi || TimelineList == null) return;
        if (TimelineList.SelectedItem is not TimelineEntry selected)
        {
            if (SelectedClipText != null) SelectedClipText.Text = "Select a clip below";
            if (TrimStartBox != null) TrimStartBox.Text = "0";
            if (ClipDurationBox != null) ClipDurationBox.Text = "0";
            return;
        }
        if (SelectedClipText != null) SelectedClipText.Text = selected.Name;
        if (TrimStartBox != null) TrimStartBox.Text = selected.Clip.TrimStartSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        if (ClipDurationBox != null) ClipDurationBox.Text = selected.Clip.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
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

    private void SplitClip_Click(object sender, RoutedEventArgs e) => SplitSelectedClip();

    private void SplitSelectedClip()
    {
        if (_exporting || TimelineList.SelectedIndex < 0 || TimelineList.SelectedIndex >= _project.Clips.Count)
            return;

        int index = TimelineList.SelectedIndex;
        var clip = _project.Clips[index];

        double currentClipTime = VideoMonitor.Position.TotalSeconds - clip.TrimStartSeconds;
        if (currentClipTime < 0.25 || currentClipTime > clip.DurationSeconds - 0.25)
        {
            SetStatus("Position playhead inside the clip (at least 0.25s from edges) to split.");
            return;
        }

        double originalDuration = clip.DurationSeconds;
        double part1Duration = Math.Round(currentClipTime, 3);
        double part2Duration = Math.Round(originalDuration - part1Duration, 3);
        double part2TrimStart = Math.Round(clip.TrimStartSeconds + part1Duration, 3);

        clip.DurationSeconds = part1Duration;

        string baseName = string.IsNullOrWhiteSpace(clip.DisplayName)
            ? System.IO.Path.GetFileNameWithoutExtension(clip.SourcePath)
            : clip.DisplayName;

        var part2 = new VideoTimelineClip
        {
            SourcePath = clip.SourcePath,
            DisplayName = $"{baseName} (Part 2)",
            TrimStartSeconds = part2TrimStart,
            DurationSeconds = part2Duration
        };

        _project.Clips.Insert(index + 1, part2);
        MarkChanged($"Split clip '{baseName}' into 2 parts");
        RefreshTimeline();
        TimelineList.SelectedIndex = index + 1;
    }

    private void MarkInFromPlayhead_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || TimelineList.SelectedIndex < 0 || TimelineList.SelectedIndex >= _project.Clips.Count)
            return;

        var clip = _project.Clips[TimelineList.SelectedIndex];
        double currentPos = VideoMonitor.Position.TotalSeconds;
        double relativePos = currentPos - clip.TrimStartSeconds;

        if (relativePos <= 0.05 || relativePos >= clip.DurationSeconds - 0.1)
            return;

        double newTrimStart = currentPos;
        double newDuration = clip.DurationSeconds - relativePos;

        clip.TrimStartSeconds = Math.Round(newTrimStart, 3);
        clip.DurationSeconds = Math.Round(newDuration, 3);
        MarkChanged("Marked In-Point");
        RefreshTimeline();
        SelectTimelineWithoutReload(TimelineList.SelectedIndex);
    }

    private void MarkOutFromPlayhead_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || TimelineList.SelectedIndex < 0 || TimelineList.SelectedIndex >= _project.Clips.Count)
            return;

        var clip = _project.Clips[TimelineList.SelectedIndex];
        double currentPos = VideoMonitor.Position.TotalSeconds;
        double relativePos = currentPos - clip.TrimStartSeconds;

        if (relativePos <= 0.1)
            return;

        clip.DurationSeconds = Math.Round(relativePos, 3);
        MarkChanged("Marked Out-Point");
        RefreshTimeline();
        SelectTimelineWithoutReload(TimelineList.SelectedIndex);
    }

    private void ResetTrim_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || TimelineList.SelectedIndex < 0 || TimelineList.SelectedIndex >= _project.Clips.Count)
            return;

        var clip = _project.Clips[TimelineList.SelectedIndex];
        var libItem = _allLibraryEntries.FirstOrDefault(x => string.Equals(x.Path, ResolveClipPath(clip), StringComparison.OrdinalIgnoreCase));
        if (libItem != null)
        {
            clip.TrimStartSeconds = 0;
            clip.DurationSeconds = libItem.DurationSeconds;
            MarkChanged("Reset clip to full duration");
            RefreshTimeline();
            SelectTimelineWithoutReload(TimelineList.SelectedIndex);
        }
    }

    private void DuplicateClip_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || TimelineList.SelectedIndex < 0 || TimelineList.SelectedIndex >= _project.Clips.Count)
            return;

        int index = TimelineList.SelectedIndex;
        var original = _project.Clips[index];
        var clone = new VideoTimelineClip
        {
            SourcePath = original.SourcePath,
            DisplayName = string.IsNullOrWhiteSpace(original.DisplayName) ? "Clip" : $"{original.DisplayName} (Copy)",
            TrimStartSeconds = original.TrimStartSeconds,
            DurationSeconds = original.DurationSeconds
        };

        _project.Clips.Insert(index + 1, clone);
        MarkChanged("Duplicated clip");
        RefreshTimeline();
        TimelineList.SelectedIndex = index + 1;
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
        RefreshTimeline();
    }

    private void TimelineZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TimelineEntry.CurrentZoomFactor = e.NewValue;
        if (_initializingUi || TimelineList == null) return;
        RefreshTimeline();
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
        if (_exporting) return;
        if (e.Data.GetData(typeof(VideoTimelineClip)) is not VideoTimelineClip source) return;
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
        RefreshTimeline();
        TimelineList.SelectedIndex = to;
    }

    private void ApplyTrim_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        if (TimelineList.SelectedItem is not TimelineEntry selected) return;
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
        var libraryDuration = _allLibraryEntries.FirstOrDefault(x =>
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
        RefreshTimeline();
        OpenClip(TimelineList.SelectedIndex, false, start);
    }

    #endregion

    #region Monitor Playback & Scrubber Controls

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
        PlayPauseIcon.Kind = PackIconKind.Play;
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
        PlayPauseIcon.Kind = PackIconKind.Pause;
    }

    private void StopPlaying()
    {
        _playing = false;
        _playbackTimer?.Stop();
        VideoMonitor?.Pause();
        if (PlayPauseIcon != null) PlayPauseIcon.Kind = PackIconKind.Play;
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

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlaying();
        if (_activeClipIndex >= 0 && _activeClipIndex < _project.Clips.Count)
        {
            VideoMonitor.Position = TimeSpan.FromSeconds(_project.Clips[_activeClipIndex].TrimStartSeconds);
            UpdatePlayheadUi();
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_project.Clips.Count == 0) return;
        if (_activeClipIndex > 0)
        {
            int prev = _activeClipIndex - 1;
            TimelineList.SelectedIndex = prev;
            OpenClip(prev, _playing, _project.Clips[prev].TrimStartSeconds);
        }
        else if (_activeClipIndex == 0)
        {
            VideoMonitor.Position = TimeSpan.FromSeconds(_project.Clips[0].TrimStartSeconds);
            UpdatePlayheadUi();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_project.Clips.Count == 0) return;
        int next = _activeClipIndex + 1;
        if (next < _project.Clips.Count)
        {
            TimelineList.SelectedIndex = next;
            OpenClip(next, _playing, _project.Clips[next].TrimStartSeconds);
        }
    }

    private void Rewind_Click(object sender, RoutedEventArgs e) => SeekRelative(-1.0);
    private void FastForward_Click(object sender, RoutedEventArgs e) => SeekRelative(1.0);

    private void SeekRelative(double deltaSeconds)
    {
        if (_activeClipIndex < 0 || _activeClipIndex >= _project.Clips.Count) return;
        var clip = _project.Clips[_activeClipIndex];
        double current = VideoMonitor.Position.TotalSeconds;
        double target = Math.Clamp(current + deltaSeconds, clip.TrimStartSeconds, clip.TrimStartSeconds + clip.DurationSeconds);
        VideoMonitor.Position = TimeSpan.FromSeconds(target);
        UpdatePlayheadUi();
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        VideoMonitor.IsMuted = !VideoMonitor.IsMuted;
        VolumeIcon.Kind = VideoMonitor.IsMuted ? PackIconKind.VolumeOff : PackIconKind.VolumeHigh;
    }

    private void MonitorVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializingUi) return;
        if (VideoMonitor != null)
        {
            VideoMonitor.Volume = e.NewValue;
            if (VolumeIcon != null && !VideoMonitor.IsMuted)
            {
                VolumeIcon.Kind = e.NewValue == 0 ? PackIconKind.VolumeMute : PackIconKind.VolumeHigh;
            }
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
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
            SelectTimelineWithoutReload(next);
            OpenClip(next, true, _project.Clips[next].TrimStartSeconds);
        }
    }

    private void UpdatePlayheadUi()
    {
        if (_activeClipIndex < 0 || _activeClipIndex >= _project.Clips.Count)
        {
            if (!_updatingSlider && PlayheadSlider != null) PlayheadSlider.Value = 0;
            if (MonitorTimeText != null) MonitorTimeText.Text = "00:00 / 00:00";
            return;
        }
        var clip = _project.Clips[_activeClipIndex];
        double clipPos = Math.Clamp(VideoMonitor.Position.TotalSeconds - clip.TrimStartSeconds,
            0, clip.DurationSeconds);

        double totalPriorSeconds = 0;
        for (int i = 0; i < _activeClipIndex; i++)
            totalPriorSeconds += _project.Clips[i].DurationSeconds;

        double overallTime = totalPriorSeconds + clipPos;
        double overallTotal = _project.Clips.Sum(c => c.DurationSeconds);

        if (overallTotal > 0 && !_updatingSlider)
        {
            _updatingSlider = true;
            try { PlayheadSlider.Value = Math.Clamp(overallTime / overallTotal, 0, 1); }
            finally { _updatingSlider = false; }
        }

        if (MonitorTimeText != null)
            MonitorTimeText.Text = $"{FormatTime(overallTime)} / {FormatTime(overallTotal)}";
    }

    private void PlayheadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializingUi || _updatingSlider || _project.Clips.Count == 0) return;
        double overallTotal = _project.Clips.Sum(c => c.DurationSeconds);
        double targetOverall = e.NewValue * overallTotal;
        double elapsed = 0;
        for (int i = 0; i < _project.Clips.Count; i++)
        {
            var clip = _project.Clips[i];
            if (targetOverall <= elapsed + clip.DurationSeconds || i == _project.Clips.Count - 1)
            {
                double within = Math.Clamp(targetOverall - elapsed, 0, clip.DurationSeconds);
                double seek = clip.TrimStartSeconds + within;
                if (i == _activeClipIndex && VideoMonitor.Source != null)
                {
                    VideoMonitor.Position = TimeSpan.FromSeconds(seek);
                }
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

    #endregion

    #region Music & Project Save/Load

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
        MarkChanged("Background music selected");
    }

    private void ClearMusic_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        _project.MusicPath = null;
        MarkChanged("Background music removed");
    }

    private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_project == null || _exporting || _initializingUi) return;
        if (Math.Abs(_project.MusicVolume - e.NewValue) > 0.001)
        {
            _project.MusicVolume = e.NewValue;
            _dirty = true;
            UpdateProjectUi();
        }
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
        RefreshTimeline();
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

    #endregion

    #region Render As... (Export)

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting || _project.Clips.Count == 0)
        {
            if (!_exporting) SetStatus("Add videos to the timeline before exporting");
            return;
        }

        var exportDialog = new RenderExportWindow(
            _project,
            _renderer,
            _presetManager,
            _logger,
            _progress,
            _cancellation)
        {
            Owner = Window.GetWindow(this)
        };

        try
        {
            _exporting = true;
            exportDialog.ShowDialog();
        }
        finally
        {
            _exporting = false;
        }
    }

    #endregion

    #region Keyboard Shortcuts

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't intercept if user is typing in a TextBox
        if (e.OriginalSource is TextBox) return;

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            PlayPause_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            SplitSelectedClip();
        }
        else if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            MarkInFromPlayhead_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            MarkOutFromPlayhead_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            DuplicateClip_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            Remove_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Left)
        {
            e.Handled = true;
            SeekRelative(Keyboard.Modifiers == ModifierKeys.Shift ? -5.0 : -1.0);
        }
        else if (e.Key == Key.Right)
        {
            e.Handled = true;
            SeekRelative(Keyboard.Modifiers == ModifierKeys.Shift ? 5.0 : 1.0);
        }
        else if (e.Key == Key.Home)
        {
            e.Handled = true;
            Previous_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.End)
        {
            e.Handled = true;
            Next_Click(this, new RoutedEventArgs());
        }
    }

    #endregion

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
        Focus();
        if (_activeClipIndex >= 0 && _activeClipIndex < _project.Clips.Count &&
            VideoMonitor.Source == null)
            OpenClip(_activeClipIndex, false, _project.Clips[_activeClipIndex].TrimStartSeconds);
    }
}
