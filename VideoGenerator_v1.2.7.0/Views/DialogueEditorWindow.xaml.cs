using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using MaterialDesignThemes.Wpf;

namespace VideoGenerator.Views
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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        public ObservableCollection<PreviewEventModel> Events { get; } = new();

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
                }
            }
        }

        public ObservableCollection<DialoguePartItem> CurrentParts { get; } = new();

        private void DialogueResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb { Tag: TextBox textBox }) return;

            double currentHeight = double.IsNaN(textBox.Height) ? textBox.ActualHeight : textBox.Height;
            textBox.Height = Math.Clamp(currentHeight + e.VerticalChange, textBox.MinHeight, textBox.MaxHeight);
        }

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
                Events.Add(ev);
            }

            EventsListBox.ItemsSource = Events;
            PartsItemsControl.ItemsSource = CurrentParts;

            if (initialSelectedEvent != null)
            {
                var match = Events.FirstOrDefault(ev => ev.FolderName == initialSelectedEvent.FolderName);
                if (match != null)
                {
                    EventsListBox.SelectedItem = match;
                    EventsListBox.ScrollIntoView(match);
                }
                else if (Events.Count > 0)
                {
                    EventsListBox.SelectedIndex = 0;
                }
            }
            else if (Events.Count > 0)
            {
                EventsListBox.SelectedIndex = 0;
            }
            
            Closed += (s, e) => {
                try { _mediaPlayer?.Close(); } catch { }
                if (!string.IsNullOrEmpty(_tempPlayWav))
                {
                    try { if (File.Exists(_tempPlayWav)) File.Delete(_tempPlayWav); } catch { }
                }
            };
        }

        private void EventsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopAudio();

            if (EventsListBox.SelectedItem is PreviewEventModel ev)
            {
                SelectedEvent = ev;
                LoadEventParts(ev);
            }
            else
            {
                SelectedEvent = null;
                CurrentParts.Clear();
            }
        }

        private void LoadEventParts(PreviewEventModel ev)
        {
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

                CurrentParts.Add(new DialoguePartItem
                {
                    AudioFilePath = audioPath,
                    Dialogue = dialoguePart,
                    IsValidated = ev.Status == "Ready" || !string.IsNullOrEmpty(dialoguePart)
                });
            }
        }

        private string _tempPlayWav = null;

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
                            // Convert on-the-fly to a temporary wav for WPF MediaPlayer compatibility
                            string tempWav = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
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
                        MessageBox.Show($"Failed to play audio: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Transcription failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        private async void ApplyEventChanges_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent == null) return;

            // Save the fields of CurrentParts back to dialogues cache & model
            var segments = CurrentParts.Select(p => p.Dialogue.Trim()).ToList();
            string combinedDialogue = string.Join(" || ", segments);

            SelectedEvent.Dialogue = combinedDialogue;
            if (SelectedEvent.ParsedData != null)
            {
                SelectedEvent.ParsedData.Dialogue = combinedDialogue;
            }

            // Save back to disk cache
            _dialogueService.SetDialogue(_language, SelectedEvent.FolderName, combinedDialogue);

            // Re-render frame frame png in background
            try
            {
                var dialogueParts = combinedDialogue.Split(new[] { "||" }, StringSplitOptions.None)
                                                    .Select(s => s.Trim())
                                                    .ToArray();
                if (dialogueParts.Length > 1 && SelectedEvent.AudioFiles.Count > 1)
                {
                    for (int i = 0; i < SelectedEvent.AudioFiles.Count; i++)
                    {
                        string partDialogue = i < dialogueParts.Length ? dialogueParts[i] : "";
                        string oldDialogue = SelectedEvent.ParsedData.Dialogue;
                        SelectedEvent.ParsedData.Dialogue = partDialogue;
                        await _imageGenerator.CreateImageAsync(SelectedEvent.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset, $"part_{i}", SelectedEvent.CharacterName);
                        SelectedEvent.ParsedData.Dialogue = oldDialogue;
                    }
                }
                else
                {
                    await _imageGenerator.CreateImageAsync(SelectedEvent.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset, "", SelectedEvent.CharacterName);
                }

                SelectedEvent.Status = "Ready";
                SelectedEvent.MarkImagesReady();
                
                // Refresh listbox trigger
                var index = EventsListBox.SelectedIndex;
                EventsListBox.SelectedIndex = -1;
                EventsListBox.SelectedIndex = index;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to regenerate preview frames: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            MessageBox.Show("Event dialogue updated and frames generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            // Auto apply changes for the current item if it was modified
            if (SelectedEvent != null && CurrentParts.Count > 0)
            {
                var segments = CurrentParts.Select(p => p.Dialogue.Trim()).ToList();
                string combinedDialogue = string.Join(" || ", segments);
                if (SelectedEvent.Dialogue != combinedDialogue)
                {
                    SelectedEvent.Dialogue = combinedDialogue;
                    if (SelectedEvent.ParsedData != null)
                    {
                        SelectedEvent.ParsedData.Dialogue = combinedDialogue;
                    }
                    _dialogueService.SetDialogue(_language, SelectedEvent.FolderName, combinedDialogue);
                    SelectedEvent.MarkImagesDirty();
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
