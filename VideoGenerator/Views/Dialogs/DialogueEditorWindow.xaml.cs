using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using MaterialDesignThemes.Wpf;

namespace VideoGenerator.Views.Dialogs
{
    public class DialoguePartItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string AudioFilePath { get; set; }
        public string PartName => Path.GetFileName(AudioFilePath);

        private string _dialogue;
        public string Dialogue
        {
            get => _dialogue;
            set => SetProperty(ref _dialogue, value);
        }

        private bool _isValidated;
        public bool IsValidated
        {
            get => _isValidated;
            set => SetProperty(ref _isValidated, value);
        }
    }

    public partial class DialogueEditorWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly TranscriptionService _transcriptionService;
        private readonly DialogueService _dialogueService;
        private readonly ImageGenerator _imageGenerator;
        private readonly VideoService _videoService;
        private readonly string _language;

        private MediaPlayer _mediaPlayer = new();
        private string _currentlyPlayingFile = null;
        private Button _currentlyPlayingButton = null;
        private string _tempPlayWav = null;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        public ObservableCollection<PreviewEventModel> Events { get; } = new();
        public ObservableCollection<PreviewEventModel> FilteredEvents { get; } = new();
        public ObservableCollection<DialoguePartItem> CurrentParts { get; } = new();

        private PreviewEventModel _selectedEvent;
        public PreviewEventModel SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (_selectedEvent != value)
                {
                    _selectedEvent = value;
                    OnPropertyChanged(nameof(SelectedEvent));
                    UpdateNavigationStates();
                }
            }
        }

        private string _selectedFilter = "ALL";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged(nameof(SelectedFilter));
                    OnPropertyChanged(nameof(IsAllFilterSelected));
                    OnPropertyChanged(nameof(IsPendingFilterSelected));
                    ApplyFilter();
                }
            }
        }

        public bool IsAllFilterSelected
        {
            get => _selectedFilter == "ALL";
            set { if (value) SelectedFilter = "ALL"; }
        }

        public bool IsPendingFilterSelected
        {
            get => _selectedFilter == "PENDING";
            set { if (value) SelectedFilter = "PENDING"; }
        }

        public int ValidatedCount => Events.Count(e => e.IsDialogueValidated);
        public int TotalCount => Events.Count;
        public int PendingCount => Events.Count(e => !e.IsDialogueValidated);

        public bool IsAllBatchValidated => TotalCount > 0 && ValidatedCount == TotalCount;

        public string ValidationProgressText =>
            TotalCount > 0
                ? $"{ValidatedCount}/{TotalCount} VALIDATED"
                : "0/0 VALIDATED";

        public string AllFilterLabel => $"ALL ({TotalCount})";
        public string PendingFilterLabel => $"PENDING ({PendingCount})";

        public string BatchReviewStatusText =>
            IsAllBatchValidated
                ? $"All {TotalCount} events are validated. Ready to finish."
                : $"{ValidatedCount} of {TotalCount} events validated ({PendingCount} pending).";

        private bool _hasPreviousEvent;
        public bool HasPreviousEvent
        {
            get => _hasPreviousEvent;
            private set
            {
                if (_hasPreviousEvent != value)
                {
                    _hasPreviousEvent = value;
                    OnPropertyChanged(nameof(HasPreviousEvent));
                }
            }
        }

        private bool _hasNextEvent;
        public bool HasNextEvent
        {
            get => _hasNextEvent;
            private set
            {
                if (_hasNextEvent != value)
                {
                    _hasNextEvent = value;
                    OnPropertyChanged(nameof(HasNextEvent));
                }
            }
        }

        public bool HasPendingEvent => PendingCount > 0;

        public DialogueEditorWindow(
            IEnumerable<PreviewEventModel> events,
            TranscriptionService transcriptionService,
            DialogueService dialogueService,
            ImageGenerator imageGenerator,
            VideoService videoService,
            string language,
            PreviewEventModel initialSelectedEvent = null)
        {
            InitializeComponent();
            _transcriptionService = transcriptionService;
            _dialogueService = dialogueService;
            _imageGenerator = imageGenerator;
            _videoService = videoService;
            _language = language ?? "EN";

            _mediaPlayer.MediaEnded += (s, e) => StopAudio();

            DataContext = this;

            foreach (var ev in events)
            {
                // Synchronize initial validation status from DialogueService
                bool isValidated = _dialogueService.IsDialogueValidated(_language, ev.FolderName);
                ev.IsDialogueValidated = isValidated;
                Events.Add(ev);
            }

            EventsListBox.ItemsSource = FilteredEvents;
            PartsItemsControl.ItemsSource = CurrentParts;

            ApplyFilter();

            if (initialSelectedEvent != null)
            {
                var match = Events.FirstOrDefault(ev => ev.FolderName == initialSelectedEvent.FolderName);
                if (match != null)
                {
                    EventsListBox.SelectedItem = match;
                    EventsListBox.ScrollIntoView(match);
                }
                else if (FilteredEvents.Count > 0)
                {
                    EventsListBox.SelectedIndex = 0;
                }
            }
            else if (FilteredEvents.Count > 0)
            {
                EventsListBox.SelectedIndex = 0;
            }

            UpdateValidationCounters();

            Closed += (s, e) => {
                try { _mediaPlayer?.Close(); } catch { }
                if (!string.IsNullOrEmpty(_tempPlayWav))
                {
                    try { if (File.Exists(_tempPlayWav)) File.Delete(_tempPlayWav); } catch { }
                }
            };
        }

        private void UpdateValidationCounters()
        {
            OnPropertyChanged(nameof(ValidatedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(IsAllBatchValidated));
            OnPropertyChanged(nameof(ValidationProgressText));
            OnPropertyChanged(nameof(AllFilterLabel));
            OnPropertyChanged(nameof(PendingFilterLabel));
            OnPropertyChanged(nameof(BatchReviewStatusText));
            OnPropertyChanged(nameof(HasPendingEvent));
            UpdateNavigationStates();
        }

        private void UpdateNavigationStates()
        {
            if (SelectedEvent == null || FilteredEvents.Count == 0)
            {
                HasPreviousEvent = false;
                HasNextEvent = false;
                return;
            }

            int index = FilteredEvents.IndexOf(SelectedEvent);
            HasPreviousEvent = index > 0;
            HasNextEvent = index >= 0 && index < FilteredEvents.Count - 1;
        }

        private void ApplyFilter()
        {
            var currentSelected = SelectedEvent;
            FilteredEvents.Clear();

            var matchingEvents = _selectedFilter == "PENDING"
                ? Events.Where(e => !e.IsDialogueValidated)
                : Events;

            foreach (var ev in matchingEvents)
            {
                FilteredEvents.Add(ev);
            }

            if (currentSelected != null && FilteredEvents.Contains(currentSelected))
            {
                EventsListBox.SelectedItem = currentSelected;
            }
            else if (FilteredEvents.Count > 0)
            {
                EventsListBox.SelectedIndex = 0;
            }
            else
            {
                SelectedEvent = null;
                CurrentParts.Clear();
            }

            UpdateNavigationStates();
        }

        private void FilterRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                ApplyFilter();
            }
        }

        private void EventsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopAudio();

            // Save previous event changes before switching
            if (e.RemovedItems != null && e.RemovedItems.Count > 0 && e.RemovedItems[0] is PreviewEventModel oldEv)
            {
                SaveEventEditsInternal(oldEv, regenerateImages: false);
            }

            if (EventsListBox.SelectedItem is PreviewEventModel newEv)
            {
                SelectedEvent = newEv;
                LoadEventParts(newEv);
            }
            else
            {
                SelectedEvent = null;
                CurrentParts.Clear();
            }

            UpdateNavigationStates();
        }

        private void LoadEventParts(PreviewEventModel ev)
        {
            foreach (var part in CurrentParts)
            {
                part.PropertyChanged -= DialoguePartItem_PropertyChanged;
            }

            CurrentParts.Clear();
            if (ev.AudioFiles == null || ev.AudioFiles.Count == 0) return;

            string rawDialogue = ev.Dialogue ?? "";
            var parts = rawDialogue.Split(new[] { "||" }, StringSplitOptions.None)
                                   .Select(p => p.Trim())
                                   .ToList();

            for (int i = 0; i < ev.AudioFiles.Count; i++)
            {
                string audioPath = ev.AudioFiles[i];
                string dialoguePart = i < parts.Count ? parts[i] : "";

                bool isPartValidated = ev.IsDialogueValidated;
                if (!isPartValidated && !string.IsNullOrEmpty(dialoguePart) && ev.Status == EventStatuses.Ready)
                {
                    // Default to validated if ready and has non-empty dialogue
                    isPartValidated = true;
                }

                var partItem = new DialoguePartItem
                {
                    AudioFilePath = audioPath,
                    Dialogue = dialoguePart,
                    IsValidated = isPartValidated
                };
                partItem.PropertyChanged += DialoguePartItem_PropertyChanged;
                CurrentParts.Add(partItem);
            }

            // Sync overall event status with parts
            if (CurrentParts.Count > 0)
            {
                bool allValidated = CurrentParts.All(p => p.IsValidated);
                if (ev.IsDialogueValidated != allValidated)
                {
                    ev.IsDialogueValidated = allValidated;
                    _dialogueService.SetDialogueValidation(_language, ev.FolderName, allValidated);
                    UpdateValidationCounters();
                }
            }
        }

        private void DialoguePartItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DialoguePartItem.IsValidated))
            {
                OnPartValidationChanged();
            }
        }

        private void PartValidatedCheckBox_Click(object sender, RoutedEventArgs e)
        {
            OnPartValidationChanged();
        }

        private void OnPartValidationChanged()
        {
            if (SelectedEvent == null || CurrentParts.Count == 0) return;

            bool allValidated = CurrentParts.All(p => p.IsValidated);
            SelectedEvent.IsDialogueValidated = allValidated;
            _dialogueService.SetDialogueValidation(_language, SelectedEvent.FolderName, allValidated);

            UpdateValidationCounters();
        }

        private void ValidateCurrentEvent_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent == null || CurrentParts.Count == 0) return;

            bool targetState = !CurrentParts.All(p => p.IsValidated);
            foreach (var part in CurrentParts)
            {
                part.IsValidated = targetState;
            }

            SelectedEvent.IsDialogueValidated = targetState;
            _dialogueService.SetDialogueValidation(_language, SelectedEvent.FolderName, targetState);
            UpdateValidationCounters();
        }

        private void PrevEvent_Click(object sender, RoutedEventArgs e)
        {
            NavigatePrevious();
        }

        private void NextEvent_Click(object sender, RoutedEventArgs e)
        {
            NavigateNext();
        }

        private void NextPending_Click(object sender, RoutedEventArgs e)
        {
            NavigateNextPending();
        }

        public void NavigatePrevious()
        {
            if (SelectedEvent == null) return;
            SaveEventEditsInternal(SelectedEvent, regenerateImages: false);

            int currentIndex = FilteredEvents.IndexOf(SelectedEvent);
            if (currentIndex > 0)
            {
                EventsListBox.SelectedIndex = currentIndex - 1;
                EventsListBox.ScrollIntoView(EventsListBox.SelectedItem);
            }
        }

        public void NavigateNext()
        {
            if (SelectedEvent == null) return;
            SaveEventEditsInternal(SelectedEvent, regenerateImages: false);

            int currentIndex = FilteredEvents.IndexOf(SelectedEvent);
            if (currentIndex >= 0 && currentIndex < FilteredEvents.Count - 1)
            {
                EventsListBox.SelectedIndex = currentIndex + 1;
                EventsListBox.ScrollIntoView(EventsListBox.SelectedItem);
            }
        }

        public void NavigateNextPending()
        {
            if (SelectedEvent != null)
            {
                SaveEventEditsInternal(SelectedEvent, regenerateImages: false);
            }

            if (Events.Count == 0) return;

            int currentIndex = SelectedEvent != null ? Events.IndexOf(SelectedEvent) : -1;
            PreviewEventModel nextPending = null;

            // Search from next item towards end
            for (int i = currentIndex + 1; i < Events.Count; i++)
            {
                if (!Events[i].IsDialogueValidated)
                {
                    nextPending = Events[i];
                    break;
                }
            }

            // Wrap around to start if not found
            if (nextPending == null)
            {
                for (int i = 0; i <= currentIndex && i < Events.Count; i++)
                {
                    if (!Events[i].IsDialogueValidated)
                    {
                        nextPending = Events[i];
                        break;
                    }
                }
            }

            if (nextPending != null)
            {
                if (_selectedFilter == "PENDING" && !FilteredEvents.Contains(nextPending))
                {
                    ApplyFilter();
                }

                EventsListBox.SelectedItem = nextPending;
                EventsListBox.ScrollIntoView(nextPending);
            }
            else
            {
                ModernMessageBox.Show("All events in the queue have been validated!", "Review Complete", MessageBoxButton.OK, MessageBoxImage.Information, owner: this);
            }
        }

        private void DialogueResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb { Tag: TextBox textBox }) return;

            double currentHeight = double.IsNaN(textBox.Height) ? textBox.ActualHeight : textBox.Height;
            textBox.Height = Math.Clamp(currentHeight + e.VerticalChange, textBox.MinHeight, textBox.MaxHeight);
        }

        private async void PlayPartAudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DialoguePartItem part)
            {
                string file = part.AudioFilePath;
                if (!File.Exists(file)) return;

                if (_currentlyPlayingFile == file)
                {
                    StopAudio();
                }
                else
                {
                    StopAudio();
                    button.IsEnabled = false;
                    try
                    {
                        await _videoService.EnsureBinariesReadyAsync();

                        string fileToPlay = file;
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext != ".wav")
                        {
                            string tempWav = AppConfig.CreateTemporaryWavPath();
                            bool convertResult = await FFMpegCore.FFMpegArguments
                                .FromFileInput(file)
                                .OutputToFile(tempWav, true, options => options.WithCustomArgument("-c:a pcm_s16le"))
                                .ProcessAsynchronously();

                            if (convertResult && File.Exists(tempWav))
                            {
                                fileToPlay = tempWav;
                                _tempPlayWav = tempWav;
                            }
                            else
                            {
                                throw new Exception("FFmpeg audio conversion failed.");
                            }
                        }

                        _mediaPlayer.Open(new Uri(fileToPlay));
                        _mediaPlayer.Play();
                        _currentlyPlayingFile = file;
                        _currentlyPlayingButton = button;

                        if (button.Content is PackIcon icon)
                        {
                            icon.Kind = PackIconKind.Stop;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModernMessageBox.Show($"Failed to play audio: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error, owner: this);
                    }
                    finally
                    {
                        button.IsEnabled = true;
                    }
                }
            }
        }

        private void StopAudio()
        {
            try
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
                if (_currentlyPlayingButton != null && _currentlyPlayingButton.Content is PackIcon icon)
                {
                    icon.Kind = PackIconKind.Play;
                }

                if (!string.IsNullOrEmpty(_tempPlayWav))
                {
                    try
                    {
                        if (File.Exists(_tempPlayWav)) File.Delete(_tempPlayWav);
                    }
                    catch { }
                    _tempPlayWav = null;
                }
            }
            catch { }
            _currentlyPlayingFile = null;
            _currentlyPlayingButton = null;
        }

        private async void TranscribeCurrentEvent_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent == null || SelectedEvent.AudioFiles.Count == 0) return;

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            try
            {
                string transcription = await _transcriptionService.TranscribeAudiosAsync(SelectedEvent.AudioFiles);
                if (!string.IsNullOrEmpty(transcription))
                {
                    SelectedEvent.Dialogue = transcription;
                    LoadEventParts(SelectedEvent);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Transcription failed: {ex.Message}", "Transcription Error", MessageBoxButton.OK, MessageBoxImage.Error, owner: this);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        private async void ApplyEventChanges_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent == null) return;

            await SaveEventEditsInternalAsync(SelectedEvent, regenerateImages: true);
            ModernMessageBox.Show("Event dialogue updated and frames generated successfully!", "Changes Applied", MessageBoxButton.OK, MessageBoxImage.Information, owner: this);
        }

        private void SaveEventEditsInternal(PreviewEventModel ev, bool regenerateImages)
        {
            if (ev == null || CurrentParts.Count == 0) return;

            var segments = CurrentParts.Select(p => p.Dialogue?.Trim() ?? "").ToList();
            string combinedDialogue = segments.All(string.IsNullOrWhiteSpace)
                ? string.Empty
                : string.Join(" || ", segments);

            ev.Dialogue = combinedDialogue;
            if (ev.ParsedData != null)
            {
                ev.ParsedData.Dialogue = combinedDialogue;
            }

            _dialogueService.SetDialogue(_language, ev.FolderName, combinedDialogue);

            bool allValidated = CurrentParts.All(p => p.IsValidated);
            ev.IsDialogueValidated = allValidated;
            _dialogueService.SetDialogueValidation(_language, ev.FolderName, allValidated);

            if (regenerateImages)
            {
                ev.MarkImagesDirty();
            }
        }

        private async Task SaveEventEditsInternalAsync(PreviewEventModel ev, bool regenerateImages)
        {
            if (ev == null) return;

            SaveEventEditsInternal(ev, regenerateImages);

            if (regenerateImages && ev.ParsedData != null)
            {
                try
                {
                    string combinedDialogue = ev.Dialogue ?? "";
                    var dialogueParts = combinedDialogue.Split(new[] { "||" }, StringSplitOptions.None)
                                                        .Select(s => s.Trim())
                                                        .ToArray();
                    if (dialogueParts.Length > 1 && ev.AudioFiles.Count > 1)
                    {
                        for (int i = 0; i < ev.AudioFiles.Count; i++)
                        {
                            string partDialogue = i < dialogueParts.Length ? dialogueParts[i] : "";
                            string oldDialogue = ev.ParsedData.Dialogue;
                            ev.ParsedData.Dialogue = partDialogue;
                            await _imageGenerator.CreateImageAsync(ev.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset, $"part_{i}", ev.CharacterName);
                            ev.ParsedData.Dialogue = oldDialogue;
                        }
                    }
                    else
                    {
                        await _imageGenerator.CreateImageAsync(ev.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset, "", ev.CharacterName);
                    }

                    ev.MarkReadyAfterProcessing();
                    ev.MarkImagesReady();
                }
                catch (Exception ex)
                {
                    ModernMessageBox.Show($"Failed to regenerate preview frames: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning, owner: this);
                }
            }

            UpdateValidationCounters();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent != null)
            {
                SaveEventEditsInternal(SelectedEvent, regenerateImages: false);
            }

            int unvalidatedCount = Events.Count(ev => !ev.IsDialogueValidated);
            if (unvalidatedCount > 0)
            {
                var result = ModernMessageBox.ShowCustom(
                    $"There are {unvalidatedCount} event(s) with unvalidated segments in this batch.\n\n" +
                    "• Validate All: Mark all remaining events as validated and finish.\n" +
                    "• Keep Unvalidated: Save and finish keeping current unvalidated statuses.\n" +
                    "• Review Pending: Return to editor and navigate to next pending event.",
                    "Unvalidated Events Warning",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    primaryButtonText: "VALIDATE ALL",
                    secondaryButtonText: "KEEP UNVALIDATED",
                    tertiaryButtonText: "REVIEW PENDING",
                    owner: this);

                if (result == MessageBoxResult.Yes)
                {
                    // Validate all events
                    foreach (var ev in Events)
                    {
                        ev.IsDialogueValidated = true;
                        _dialogueService.SetDialogueValidation(_language, ev.FolderName, true);
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    NavigateNextPending();
                    return;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
