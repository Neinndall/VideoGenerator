using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;
using System.Collections.Generic;

namespace VideoGenerator.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardModel _model = new();
        private readonly DatabaseBuilder _databaseBuilder;
        private readonly TaskCancellationService _cancellationService;
        private readonly TranslationService _translationService;
        private readonly ImageGenerator _imageGenerator;
        private readonly VideoService _videoService;
        private readonly LogService _logger;
        private readonly TranscriptionService _transcriptionService;
        private readonly DialogueService _dialogueService;
        private readonly EventFilterService _eventFilterService;
        private readonly ProgressService _progressService;
        private readonly EventRulesView _eventRulesView;
        private readonly EventAnalysisService _eventAnalysisService;
        private readonly EventIconResolutionService _eventIconResolutionService;
        private readonly PreviewImageService _previewImageService;
        private readonly AudioFamilyMergeService _audioFamilyMergeService;
        private readonly HudImagePreparationService _hudImagePreparationService;
        private readonly ProductionWorkPlanningService _productionWorkPlanningService;

        public DashboardView(
            DatabaseBuilder databaseBuilder,
            TranslationService translationService,
            ImageGenerator imageGenerator,
            VideoService videoService,
            LogService logger,
            TranscriptionService transcriptionService,
            DialogueService dialogueService,
            TaskCancellationService cancellationService,
            EventFilterService eventFilterService,
            ProgressService progressService,
            EventRulesView eventRulesView,
            EventAnalysisService eventAnalysisService,
            EventIconResolutionService eventIconResolutionService,
            PreviewImageService previewImageService,
            AudioFamilyMergeService audioFamilyMergeService,
            HudImagePreparationService hudImagePreparationService,
            ProductionWorkPlanningService productionWorkPlanningService)
        {
            InitializeComponent();
            _databaseBuilder = databaseBuilder;
            _translationService = translationService;
            _imageGenerator = imageGenerator;
            _videoService = videoService;
            _logger = logger;
            _transcriptionService = transcriptionService;
            _dialogueService = dialogueService;
            _cancellationService = cancellationService;
            _eventFilterService = eventFilterService;
            _progressService = progressService;
            _eventRulesView = eventRulesView;
            _eventAnalysisService = eventAnalysisService;
            _eventIconResolutionService = eventIconResolutionService;
            _previewImageService = previewImageService;
            _audioFamilyMergeService = audioFamilyMergeService;
            _hudImagePreparationService = hudImagePreparationService;
            _productionWorkPlanningService = productionWorkPlanningService;
            
            DataContext = this;
            DashboardModel = _model;
            LogService = _logger;

            InitializeData();
        }

        public DashboardModel DashboardModel { get; }
        public LogService LogService { get; }

        private async Task EnsureAudioFamiliesMergedAsync(
            PreviewEventModel ev,
            CancellationToken token,
            Action<AudioFamilyModel> onFamilyMerged = null)
        {
            if (onFamilyMerged == null)
                _progressService.Report(0);

            int completedFamilies = 0;
            int totalFamilies = ev.AudioFamilies.Count;
            await _audioFamilyMergeService.MergeAsync(
                ev,
                token,
                family =>
                {
                    if (onFamilyMerged != null)
                    {
                        onFamilyMerged(family);
                        return;
                    }

                    completedFamilies++;
                    double progress = (double)completedFamilies / totalFamilies * 100.0;
                    _progressService.Report(progress, $"Merged audio family: {family.Name} ({progress:0}%)");
                },
                status => _progressService.SetStatus(status));
        }

        private async Task EnsureAudioFamiliesMergedAsync(
            IReadOnlyList<PreviewEventModel> events,
            CancellationToken token,
            Action<AudioFamilyModel> onFamilyMerged = null)
        {
            var pendingEvents = events.Where(_audioFamilyMergeService.IsPending).ToList();
            int totalFamilies = pendingEvents.Sum(ev => ev.AudioFamilies.Count);
            if (totalFamilies == 0) return;

            int completedFamilies = 0;
            _progressService.Report(0);

            await _audioFamilyMergeService.MergeAsync(
                (IReadOnlyList<PreviewEventModel>)pendingEvents,
                token,
                family =>
                {
                    if (onFamilyMerged != null)
                    {
                        onFamilyMerged(family);
                        return;
                    }

                    completedFamilies++;
                    double progress = (double)completedFamilies / totalFamilies * 100.0;
                    _progressService.Report(progress, $"Merging audio families: {completedFamilies}/{totalFamilies} ({progress:0}%)");
                },
                status => _progressService.SetStatus(status));
        }

        private void InitializeData()
        {
            _model.AudioPath = AppSettings.Instance.MediaSourceDirectory;
            
            // Initialize CharactersList with "ALL"
            _model.CharactersList.Clear();
            _model.CharactersList.Add("ALL");
            _model.SelectedCharacter = "ALL";

            AppSettings.Instance.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(AppSettings.MediaSourceDirectory))
                {
                    _model.AudioPath = AppSettings.Instance.MediaSourceDirectory;
                }
                else if (e.PropertyName == nameof(AppSettings.CustomBackgroundPath) ||
                         e.PropertyName == nameof(AppSettings.SelectedFontName) ||
                         e.PropertyName == nameof(AppSettings.TextVerticalOffset) ||
                         e.PropertyName == nameof(AppSettings.IconAlignment) ||
                         e.PropertyName == nameof(AppSettings.IconVerticalOffset) ||
                         e.PropertyName == nameof(AppSettings.EnableTranscriptions) ||
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
                    await UpdatePreviewAsync();
                }
            };

            _model.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(_model.SelectedEvent) && _model.SelectedEvent != null)
                {
                    await UpdatePreviewAsync();
                }
                else if (e.PropertyName == nameof(_model.SelectedFilter) || 
                         e.PropertyName == nameof(_model.SelectedCharacter) ||
                         e.PropertyName == nameof(_model.SearchQuery))
                {
                    ApplyFilter();
                }
            };

            _model.ProcessedEvents.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    foreach (PreviewEventModel ev in e.NewItems)
                    {
                        if (!string.IsNullOrEmpty(ev.CharacterName) && !_model.CharactersList.Contains(ev.CharacterName))
                        {
                            // Insert alphabetically after "ALL" (index 0)
                            int insertIdx = 1;
                            while (insertIdx < _model.CharactersList.Count && string.Compare(_model.CharactersList[insertIdx], ev.CharacterName, StringComparison.OrdinalIgnoreCase) < 0)
                                insertIdx++;
                            
                            if (insertIdx <= _model.CharactersList.Count)
                                _model.CharactersList.Insert(insertIdx, ev.CharacterName);
                            else
                                _model.CharactersList.Add(ev.CharacterName);
                        }

                        var filter = _model.SelectedFilter;
                        var characterFilter = _model.SelectedCharacter ?? "ALL";

                        if (characterFilter == "ALL" || string.Equals(ev.CharacterName, characterFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            bool include = filter switch
                            {
                                "ERRORS" => ev.Status == "Missing Icon" || ev.Status == "No Audio",
                                "PENDING" => ev.Status == "Pending",
                                _ => true
                            };

                            if (include) _model.FilteredProcessedEvents.Add(ev);
                        }
                    }
                    if (_model.SelectedEvent == null && _model.FilteredProcessedEvents.Count > 0)
                        _model.SelectedEvent = _model.FilteredProcessedEvents[0];
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (PreviewEventModel ev in e.OldItems)
                    {
                        _model.FilteredProcessedEvents.Remove(ev);
                        if (_model.SelectedEvent == ev) _model.SelectedEvent = _model.FilteredProcessedEvents.FirstOrDefault();
                    }
                }
            };
        }

        private void ApplyFilter()
        {
            var characterFilter = _model.SelectedCharacter ?? "ALL";
            var filter = _model.SelectedFilter;
            var search = _model.SearchQuery;

            var items = _eventFilterService.FilterEvents(
                _model.ProcessedEvents, 
                characterFilter, 
                filter, 
                search);

            var previouslySelected = _model.SelectedEvent;

            _model.FilteredProcessedEvents.Clear();
            foreach (var item in items) _model.FilteredProcessedEvents.Add(item);

            if (previouslySelected != null && _model.FilteredProcessedEvents.Contains(previouslySelected))
            {
                _model.SelectedEvent = previouslySelected;
            }
            else if (_model.FilteredProcessedEvents.Count > 0)
            {
                _model.SelectedEvent = _model.FilteredProcessedEvents[0];
            }
            else
            {
                _model.SelectedEvent = null;
            }

            if (_model.SelectedEvent != null)
            {
                EventsListBox?.ScrollIntoView(_model.SelectedEvent);
            }
        }

        private void EventsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EventsListBox != null && EventsListBox.SelectedItem != null)
            {
                EventsListBox.ScrollIntoView(EventsListBox.SelectedItem);
            }
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true && rb.Tag != null)
                _model.SelectedFilter = rb.Tag.ToString();
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is PreviewEventModel ev)
            {
                _logger.LogInfo($"Removing event: {ev.FolderName}");
                _model.ProcessedEvents.Remove(ev);
            }
        }

        private async void ApplyQuickEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null || _model.SelectedEvent.ParsedData == null) return;
            var ev = _model.SelectedEvent;
            string text = QuickEditDisplayText.Text;
            string iconName = QuickEditIconLookup.Text;
            string iconType = QuickEditIconType.SelectedValue?.ToString() ?? "generic";
            string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";

            if (iconType == "item" && !string.IsNullOrEmpty(iconName))
            {
                string resolvedId = await _eventIconResolutionService.ResolveItemNameToIdAsync(iconName);
                if (resolvedId != null) iconName = resolvedId;
            }

            bool dialogueChanged = !string.Equals(ev.Dialogue, QuickEditDialogue.Text, StringComparison.Ordinal);
            bool visualDataChanged = !string.Equals(ev.ParsedData.DisplayText, text, StringComparison.Ordinal) ||
                                     !string.Equals(ev.ParsedData.IconLookupName, iconName, StringComparison.Ordinal) ||
                                     !string.Equals(ev.ParsedData.IconType, iconType, StringComparison.Ordinal);

            ev.ParsedData.DisplayText = text;
            ev.ParsedData.IconLookupName = iconName;
            ev.ParsedData.IconType = iconType;

            string iconPath = await _eventIconResolutionService.ResolveAsync(ev.ParsedData);

            ev.ParsedData.IconPath = iconPath;
            ev.Status = string.IsNullOrEmpty(iconPath) && iconType != "generic" ? "Missing Icon" : "Ready";

            string dialogue = QuickEditDialogue.Text;
            ev.Dialogue = dialogue;
            if (ev.ParsedData != null)
            {
                ev.ParsedData.Dialogue = dialogue;
            }
            if (dialogueChanged || visualDataChanged)
            {
                ev.MarkImagesDirty();
            }
            _dialogueService.SetDialogue(selectedLang, ev.FolderName, dialogue);

            await UpdatePreviewAsync();
        }

        private void QuickMap_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null) return;
            _eventRulesView.PreFillFromDashboard(_model.SelectedEvent.FolderName);

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateTo("EventRules");
            }
        }

        // CancelActiveOperation removed, managed globally via TaskCancellationService

        private void HandleCancellation()
        {
            _progressService.Cancel();
            _logger.LogWarn("TASK CANCELED - OPERATION ABORTED BY USER.");
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Audio Directory" };
            if (dialog.ShowDialog() == true)
            {
                _model.AudioPath = dialog.FolderName;
                _model.IsAnalyzed = false;
                _model.ProcessedEvents.Clear();
                _model.FilteredProcessedEvents.Clear();
                _model.CharactersList.Clear();
                _model.CharactersList.Add("ALL");
                ResetEventFilters();
            }
        }

        private void ResetEventFilters()
        {
            _model.SelectedFilter = "ALL";
            _model.SearchQuery = string.Empty;
            _model.SelectedCharacter = "ALL";
        }

        private async void ProcessFolders_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_model.AudioPath) || !Directory.Exists(_model.AudioPath))
            {
                _logger.LogError("Invalid audio path selected.");
                return;
            }

            var token = _cancellationService.CreateNewToken();

            _model.IsProcessing = true;
            _progressService.Start("Waiting for official data synchronization", true);
            _model.ProcessedEvents.Clear();
            _model.FilteredProcessedEvents.Clear();
            _model.CharactersList.Clear();
            _model.CharactersList.Add("ALL");
            ResetEventFilters();
            _logger.Logs.Clear();
            _logger.LogInfo($">>> ANALYZING: {_model.AudioPath}");

            string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";

            try {
                await _databaseBuilder.ReadyTask.WaitAsync(token);
                token.ThrowIfCancellationRequested();
                _progressService.Start("Analyzing audio folders", false);

                var analyzedEvents = await _eventAnalysisService.AnalyzeAsync(
                    _model.AudioPath,
                    selectedLang,
                    status => _progressService.SetStatus(status),
                    value => _progressService.Report(value),
                    token);

                foreach (PreviewEventModel pipelineEvent in analyzedEvents)
                {
                    _model.ProcessedEvents.Add(pipelineEvent);
                }
                _model.IsAnalyzed = _model.ProcessedEvents.Count > 0;
                if (_model.ProcessedEvents.Count > 0) _model.SelectedEvent = _model.ProcessedEvents[0];
                _logger.LogInfo($">>> ANALYSIS COMPLETE. Found {_model.ProcessedEvents.Count} events. Resolving icons...");
                // Keep 100% visible briefly before switching
                await Task.Delay(400, token);

                // Resolve icons concurrently and await to keep the progress bar active
                await ResolvePendingIconsAsync(token);
                token.ThrowIfCancellationRequested();
            } catch (OperationCanceledException) {
                HandleCancellation();
            } catch (Exception ex) { 
                _logger.LogError("Critical analysis failure", ex); 
            }
            finally
            {
                _model.IsProcessing = false;
                await _progressService.CompleteAsync();
            }
        }

        private async void PrepareTranscription_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.IsAnalyzed || _model.FilteredProcessedEvents.Count == 0) return;
            
            var token = _cancellationService.CreateNewToken();

            _model.IsProcessing = true;
            _logger.LogInfo(">>> STARTING BATCH PREPARATION (STAGE 1)...");
            try {
                var eventsToPrepare = _model.FilteredProcessedEvents.ToList();
                string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";

                await Task.Run(async () => {
                    PreparationWorkPlan workPlan = _productionWorkPlanningService.CreatePreparationPlan(eventsToPrepare);
                    var workSummary = new List<string>
                    {
                        $"{workPlan.TranscriptionWork} audio",
                        $"{workPlan.ImageWork} HUD"
                    };
                    if (workPlan.IconWork > 0) workSummary.Add($"{workPlan.IconWork} icons");
                    if (workPlan.MergeWork > 0) workSummary.Add($"{workPlan.MergeWork} merge");
                    _progressService.StartWork(
                        $"Preparing: {string.Join(" + ", workSummary)}",
                        workPlan.TotalWork);

                    await EnsureAudioFamiliesMergedAsync(eventsToPrepare, token, family =>
                    {
                        _progressService.Advance(
                            family.AudioFiles.Count,
                            $"Merged audio: {family.Name}");
                    });

                    foreach (var ev in eventsToPrepare) {
                        token.ThrowIfCancellationRequested();
                        _progressService.SetStatus($"Preparing: {ev.FolderName}");

                        // 1. Resolve pending icon
                        if (ev.ParsedData != null && ev.ParsedData.IconType != "generic" && string.IsNullOrEmpty(ev.ParsedData.IconPath))
                        {
                            _progressService.SetStatus($"Resolving icon for: {ev.FolderName}");
                            string iconPath = await _eventIconResolutionService.ResolveAsync(ev.ParsedData);
                            if (string.IsNullOrEmpty(iconPath))
                            {
                                _logger.LogWarn($"Failed to resolve icon for event '{ev.FolderName}' ({ev.ParsedData.IconType}:{ev.ParsedData.IconLookupName}). The event will remain without an icon.");
                            }
                            token.ThrowIfCancellationRequested();
                            Application.Current.Dispatcher.Invoke(() => {
                                ev.ParsedData.IconPath = string.IsNullOrEmpty(iconPath) ? "MISSING" : iconPath;
                                ev.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
                            });
                            _progressService.Advance(1, $"Resolved icon: {ev.FolderName}");
                        }

                        if (ev.Status == "No Audio" || ev.Status == "Missing Icon" || ev.ParsedData == null)
                        {
                            int skippedWork = workPlan.TranscriptionWorkByEvent[ev] + workPlan.ImageWorkByEvent[ev];
                            if (skippedWork > 0)
                                _progressService.Advance(skippedWork, $"Skipped invalid event: {ev.FolderName}");
                            continue;
                        }

                        string dialogue = ev.Dialogue;
                        bool shouldTranscribe = AppSettings.Instance.EnableTranscriptions && ev.AudioFiles.Count > 0 &&
                            (string.IsNullOrEmpty(dialogue) || AppSettings.Instance.ForceBatchRetranscribe);

                        if (shouldTranscribe)
                        {
                            string transcription = await _transcriptionService.TranscribeAudiosAsync(ev.AudioFiles, 
                                (audioPath) => {
                                    _progressService.SetStatus($"Transcribing: {Path.GetFileName(audioPath)}");
                                },
                                (audioPath) =>
                                {
                                    _progressService.Advance(
                                        1,
                                        $"Transcribed: {Path.GetFileName(audioPath)}");
                                },
                                token);
                            token.ThrowIfCancellationRequested();
                            if (!string.IsNullOrEmpty(transcription))
                            {
                                dialogue = transcription;
                                ev.Dialogue = transcription;
                                if (ev.ParsedData != null)
                                {
                                    ev.ParsedData.Dialogue = transcription;
                                }

                                string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";
                                _dialogueService.SetDialogue(selectedLang, ev.FolderName, transcription);

                                if (_model.SelectedEvent == ev)
                                {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        QuickEditDialogue.Text = transcription;
                                    });
                                }
                            }
                        }
                        else if (AppSettings.Instance.CleanWhisperHallucinations && !string.IsNullOrEmpty(dialogue))
                        {
                            string cleaned = DialogueService.CleanDialogue(dialogue);
                            if (cleaned != dialogue)
                            {
                                dialogue = cleaned;
                                ev.Dialogue = cleaned;
                                if (ev.ParsedData != null)
                                {
                                    ev.ParsedData.Dialogue = cleaned;
                                }
                                string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";
                                _dialogueService.SetDialogue(selectedLang, ev.FolderName, cleaned);

                                if (_model.SelectedEvent == ev)
                                {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        QuickEditDialogue.Text = cleaned;
                                    });
                                }
                            }
                        }

                        IReadOnlyList<string> imagePaths = await _hudImagePreparationService.PrepareAsync(
                            ev,
                            dialogue,
                            reuseExistingImages: false,
                            cancellationToken: token,
                            status => _progressService.SetStatus(status),
                            (work, status) => _progressService.Advance(work, status));
                        int generatedImages = imagePaths.Count;

                        int unusedImageBudget = workPlan.ImageWorkByEvent[ev] - generatedImages;
                        if (unusedImageBudget > 0)
                        {
                            _progressService.Advance(unusedImageBudget, $"Completed image set: {ev.FolderName}");
                        }

                        token.ThrowIfCancellationRequested();
                        Application.Current.Dispatcher.Invoke(() => {
                            ev.Status = "Ready";
                        });

                    }
                    _progressService.FinishWork("Preparation complete - opening dialogue editor");
                }, token);
                _logger.LogInfo(">>> BATCH PREPARATION COMPLETE. You can now REVIEW DIALOGUES or RENDER VIDEOS.");

                // Show DialogueEditor Window automatically once preparation completes
                Application.Current.Dispatcher.Invoke(() => {
                    ReviewDialogues_Click(null, null);
                });
            } catch (OperationCanceledException) {
                HandleCancellation();
            } catch (Exception ex) { 
                _logger.LogError("Preparation failed", ex); 
            }
            finally
            {
                _model.IsProcessing = false;
                await _progressService.CompleteAsync();
            }
        }

        private async void ReviewDialogues_Click(object sender, RoutedEventArgs e)
        {
            var events = _model.FilteredProcessedEvents.ToList();
            if (events.Count == 0) return;

            bool hasPendingFamilies = events.Any(ev =>
                AppSettings.Instance.MergeAudioFamilies &&
                ev.AudioFamilies.Count > 0 &&
                !ev.AreAudioFamiliesMerged);
            int pendingSourceAudios = events
                .Where(ev => AppSettings.Instance.MergeAudioFamilies &&
                             ev.AudioFamilies.Count > 0 &&
                             !ev.AreAudioFamiliesMerged)
                .SelectMany(ev => ev.AudioFamilies)
                .Sum(family => family.AudioFiles.Count);
            bool ownsProcessingState = hasPendingFamilies && !_model.IsProcessing;
            CancellationToken mergeToken = ownsProcessingState
                ? _cancellationService.CreateNewToken()
                : CancellationToken.None;

            try
            {
                if (ownsProcessingState)
                {
                    _model.IsProcessing = true;
                    _progressService.StartWork($"Review: merging {pendingSourceAudios} audio", pendingSourceAudios);
                }

                if (ownsProcessingState)
                {
                    await EnsureAudioFamiliesMergedAsync(events, mergeToken, family =>
                        _progressService.Advance(
                            family.AudioFiles.Count,
                            $"Merged audio: {family.Name}"));
                    _progressService.FinishWork("Audio families ready - opening dialogue editor");
                }
                else
                {
                    await EnsureAudioFamiliesMergedAsync(events, mergeToken);
                }

                if (ownsProcessingState)
                    await Task.Delay(250, mergeToken);
            }
            catch (OperationCanceledException)
            {
                HandleCancellation();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to prepare audio families for dialogue review", ex);
                return;
            }
            finally
            {
                if (ownsProcessingState)
                {
                    _model.IsProcessing = false;
                    await _progressService.CompleteAsync();
                }
            }

            var dialog = new DialogueEditorWindow(
                events,
                _transcriptionService,
                _dialogueService,
                _imageGenerator,
                _videoService,
                AppSettings.Instance.DefaultDictionaryLanguage ?? "EN",
                _model.SelectedEvent
            );
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                _logger.LogInfo("Dialogues reviewed and approved by user.");
                _ = UpdatePreviewAsync(); // Refresh current preview
            }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.IsAnalyzed || _model.FilteredProcessedEvents.Count == 0) return;
            
            var token = _cancellationService.CreateNewToken();

            _model.IsProcessing = true;
            _logger.LogInfo(">>> STARTING BATCH VIDEO RENDERING (STAGE 2)...");
            try {
                var eventsToRender = _model.FilteredProcessedEvents.ToList();

                await Task.Run(async () => {
                    double silenceDuration = AppSettings.Instance.SilenceDuration;
                    RenderWorkPlan workPlan = _productionWorkPlanningService.CreateRenderPlan(eventsToRender);
                    _progressService.StartWork("Rendering videos", workPlan.TotalWork);

                    await EnsureAudioFamiliesMergedAsync(eventsToRender, token, family =>
                        _progressService.Advance(
                            family.AudioFiles.Count,
                            $"Merged family: {family.Name}"));

                    foreach (var ev in eventsToRender) {
                        token.ThrowIfCancellationRequested();
                        _progressService.SetStatus($"Rendering video: {ev.FolderName}");

                        if (ev.Status == "No Audio" || ev.Status == "Missing Icon" || ev.ParsedData == null || ev.AudioFiles.Count == 0)
                            continue;

                        string dialogue = ev.Dialogue;
                        IReadOnlyList<string> imagePaths = await _hudImagePreparationService.PrepareAsync(
                            ev,
                            dialogue,
                            reuseExistingImages: true,
                            cancellationToken: token,
                            status => _progressService.SetStatus(status),
                            (work, status) => _progressService.Advance(work, status));

                        token.ThrowIfCancellationRequested();
                        _progressService.SetStatus($"Compiling video for {ev.FolderName}");

                        // Compile video Frame + Audio using FFmpeg
                        string outputVideoDir = Path.Combine(AppConfig.OutputVideosDir, ev.CharacterName);
                        DirectoriesCreator.CreateDirectory(outputVideoDir);
                        string outputPath = Path.Combine(outputVideoDir, ev.FolderName + ".mp4");
                        
                        bool rendered = await _videoService.CreateVideoAsync(
                            imagePaths.ToList(),
                            ev.AudioFiles,
                            outputPath,
                            silenceDuration,
                            dialogue,
                            step => _progressService.Advance(1, $"{step}: {ev.FolderName}"),
                            token);
                        if (!rendered)
                            throw new InvalidOperationException($"Video rendering did not complete for {ev.FolderName}.");
                    }
                    _progressService.FinishWork("Video rendering complete");
                }, token);
                
                token.ThrowIfCancellationRequested();
                _logger.LogInfo(">>> VIDEO RENDERING COMPLETED.");
            } catch (OperationCanceledException) {
                HandleCancellation();
            } catch (Exception ex) { 
                _logger.LogError("Rendering failed", ex); 
            }
            finally
            {
                _model.IsProcessing = false;
                await _progressService.CompleteAsync();
            }
        }

        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null || _model.SelectedEvent.AudioFiles.Count == 0) return;
            var ev = _model.SelectedEvent;
            
            _logger.LogInfo($"Starting transcription for: {ev.FolderName}");
            
            var token = _cancellationService.CreateNewToken();

            _model.IsProcessing = true;
            _progressService.Start($"Transcribing: {ev.FolderName}");
            try
            {
                await EnsureAudioFamiliesMergedAsync(ev, token);
                if (ev.AudioFiles != null && ev.AudioFiles.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string transcription = await _transcriptionService.TranscribeAudiosAsync(ev.AudioFiles, cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    if (!string.IsNullOrEmpty(transcription))
                    {
                        ev.Dialogue = transcription;
                        if (ev.ParsedData != null)
                        {
                            ev.ParsedData.Dialogue = transcription;
                        }
                        ev.MarkImagesDirty();
                        QuickEditDialogue.Text = transcription;
                        string selectedLang = AppSettings.Instance.DefaultDictionaryLanguage ?? "EN";
                        _dialogueService.SetDialogue(selectedLang, ev.FolderName, transcription);
                        _logger.LogInfo($"Successfully transcribed: {transcription}");
                        await UpdatePreviewAsync();
                    }
                    else
                    {
                        _logger.LogWarn("Transcription returned empty or failed.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                HandleCancellation();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to auto-transcribe audio", ex);
            }
            finally
            {
                _model.IsProcessing = false;
                await _progressService.CompleteAsync();
            }
        }

    }
}
