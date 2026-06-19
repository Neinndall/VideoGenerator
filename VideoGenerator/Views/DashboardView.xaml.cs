using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;

namespace VideoGenerator.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardModel _model = new();
        private readonly DataFetcher _dataFetcher;
        private CancellationTokenSource _previewCancellationSource;
        private readonly TranslationService _translationService;
        private readonly IconManager _iconManager;
        private readonly NameParser _nameParser;
        private readonly ImageGenerator _imageGenerator;
        private readonly VideoService _videoService;
        private readonly LogService _logger;
        private readonly TranscriptionService _transcriptionService;
        private readonly DialogueService _dialogueService;

        public DashboardView(
            DataFetcher dataFetcher,
            TranslationService translationService,
            IconManager iconManager,
            NameParser nameParser,
            ImageGenerator imageGenerator,
            VideoService videoService,
            LogService logger,
            TranscriptionService transcriptionService,
            DialogueService dialogueService)
        {
            InitializeComponent();
            _dataFetcher = dataFetcher;
            _translationService = translationService;
            _iconManager = iconManager;
            _nameParser = nameParser;
            _imageGenerator = imageGenerator;
            _videoService = videoService;
            _logger = logger;
            _transcriptionService = transcriptionService;
            _dialogueService = dialogueService;
            
            DataContext = this;
            DashboardModel = _model;
            LogService = _logger;

            InitializeData();
        }

        public DashboardModel DashboardModel { get; }
        public LogService LogService { get; }

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
                         e.PropertyName == nameof(AppSettings.BubbleHorizontalOffset))
                {
                    await UpdatePreviewAsync();
                }
            };

            foreach (var lang in _translationService.AvailableLanguages)
                _model.AvailableLanguages.Add(lang);

            if (_model.AvailableLanguages.Count > 0)
                _model.SelectedLanguage = _model.AvailableLanguages.Contains("EN") ? "EN" : _model.AvailableLanguages[0];

            _model.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(_model.SelectedEvent) && _model.SelectedEvent != null)
                {
                    await UpdatePreviewAsync();
                }
                else if (e.PropertyName == nameof(_model.SelectedFilter) || e.PropertyName == nameof(_model.SelectedCharacter))
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

            var items = _model.ProcessedEvents.Where(ev => {
                bool matchesChar = characterFilter == "ALL" || string.Equals(ev.CharacterName, characterFilter, StringComparison.OrdinalIgnoreCase);
                if (!matchesChar) return false;
                        return filter switch
                            {
                                "ERRORS" => ev.Status == "Missing Icon" || ev.Status == "No Audio",
                                "PENDING" => ev.Status == "Pending" || ev.Status == "Pending Icon",
                                _ => true
                            };
            }).ToList();

            _model.FilteredProcessedEvents.Clear();
            foreach (var item in items) _model.FilteredProcessedEvents.Add(item);
        }

        private async Task UpdatePreviewAsync()
        {
            if (_model.SelectedEvent == null) return;

            // Cancel any pending preview generation
            _previewCancellationSource?.Cancel();
            _previewCancellationSource = new CancellationTokenSource();
            var token = _previewCancellationSource.Token;

            try
            {
                // Debounce rapid keyboard navigation/clicks by 50ms
                await Task.Delay(50, token);

                // 1. Draw the preview immediately (so text/bubble appears instantly)
                await GeneratePreviewAsync(_model.SelectedEvent, token);
                if (token.IsCancellationRequested) return;

                // 2. Resolve the icon in the background if it's missing
                var ev = _model.SelectedEvent;
                if (ev.ParsedData != null && ev.ParsedData.IconType != "generic" && string.IsNullOrEmpty(ev.ParsedData.IconPath))
                {
                    await ResolveEventIconAsync(ev);
                    if (token.IsCancellationRequested) return;

                    // 3. Re-draw preview now with the resolved icon
                    await GeneratePreviewAsync(ev, token);
                }
            }
            catch (TaskCanceledException)
            {
                // Selection changed, task cancelled as expected
            }
        }

        private async Task GeneratePreviewAsync(PreviewEventModel ev, CancellationToken token)
        {
            try
            {
                if (ev.ParsedData == null) return;
                if (token.IsCancellationRequested) return;

                var bytes = await Task.Run(async () => 
                    await _imageGenerator.CreateImageBytesAsync(ev.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset),
                    token);
                if (token.IsCancellationRequested) return;

                if (bytes != null)
                {
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(bytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze(); // Make read-only and thread-safe

                    if (token.IsCancellationRequested) return;

                    Application.Current.Dispatcher.Invoke(() => {
                        _model.PreviewImageSource = bitmap;
                    });
                }
            }
            catch (Exception ex) 
            { 
                if (!token.IsCancellationRequested)
                    _logger.LogError("Preview generation failed", ex); 
            }
        }

        private async Task ResolveEventIconAsync(PreviewEventModel ev)
        {
            if (ev?.ParsedData == null || ev.ParsedData.IconType == "generic" || !string.IsNullOrEmpty(ev.ParsedData.IconPath))
                return;

            string iconPath = await ResolveIconPathAsync(ev.ParsedData);
            ev.ParsedData.IconPath = string.IsNullOrEmpty(iconPath) ? "MISSING" : iconPath;
            ev.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
        }

        private async Task<string> ResolveIconPathAsync(ParsedEvent parsedEvent)
        {
            if (parsedEvent == null || parsedEvent.IconType == "generic")
                return null;

            try
            {
                string lolVersion = null;
                if (parsedEvent.IconType is "champion" or "region")
                    lolVersion = await _dataFetcher.GetLatestLolVersionAsync();

                return parsedEvent.IconType switch
                {
                    "champion" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                    "item" => await _iconManager.GetItemIconAsync(parsedEvent.IconLookupName),
                    "monster" => await _iconManager.GetMonsterIconAsync(parsedEvent.IconLookupName),
                    "region" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                    "structure" => await _iconManager.GetStructureIconAsync(parsedEvent.IconLookupName),
                    "system" => await _iconManager.GetSystemIconAsync(parsedEvent.IconLookupName),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Icon resolution failed for {parsedEvent.IconType}:{parsedEvent.IconLookupName}", ex);
                return null;
            }
        }

        private async Task ResolvePendingIconsAsync()
        {
            var pending = _model.ProcessedEvents
                .Where(ev => ev.ParsedData != null && ev.ParsedData.IconType != "generic" && string.IsNullOrEmpty(ev.ParsedData.IconPath))
                .ToList();

            if (pending.Count == 0) return;

            var semaphore = new SemaphoreSlim(3, 3);
            var tasks = pending.Select(async ev =>
            {
                await semaphore.WaitAsync();
                try
                {
                    string iconPath = await ResolveIconPathAsync(ev.ParsedData);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ev.ParsedData.IconPath = iconPath;
                        ev.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Background icon resolution failed for {ev.FolderName}", ex);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
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
            string selectedLang = _model.SelectedLanguage ?? "EN";

            if (iconType == "item" && !string.IsNullOrEmpty(iconName))
            {
                string resolvedId = await _dataFetcher.ResolveItemNameToIdAsync(iconName);
                if (resolvedId != null) iconName = resolvedId;
            }

            ev.ParsedData.DisplayText = text;
            ev.ParsedData.IconLookupName = iconName;
            ev.ParsedData.IconType = iconType;

            var lolVersion = await _dataFetcher.GetLatestLolVersionAsync();
            string iconPath = iconType switch {
                "item" => await _iconManager.GetItemIconAsync(iconName),
                "monster" => await _iconManager.GetMonsterIconAsync(iconName),
                "champion" => await _iconManager.GetChampionIconAsync(iconName, lolVersion),
                "region" => await _iconManager.GetChampionIconAsync(iconName, lolVersion),
                "structure" => await _iconManager.GetStructureIconAsync(iconName),
                "system" => await _iconManager.GetSystemIconAsync(iconName),
                _ => null
            };

            ev.ParsedData.IconPath = iconPath;
            ev.Status = string.IsNullOrEmpty(iconPath) && iconType != "generic" ? "Missing Icon" : "Ready";

            string dialogue = QuickEditDialogue.Text;
            ev.Dialogue = dialogue;
            if (ev.ParsedData != null)
            {
                ev.ParsedData.Dialogue = dialogue;
            }
            _dialogueService.SetDialogue(selectedLang, ev.FolderName, dialogue);

            await UpdatePreviewAsync();
        }

        private void QuickMap_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null) return;
            var rulesView = App.ServiceProvider.GetRequiredService<EventRulesView>();
            rulesView.PreFillFromDashboard(_model.SelectedEvent.FolderName);

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var navMethod = mainWindow.GetType().GetMethod("NavigateTo", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                navMethod?.Invoke(mainWindow, new object[] { "EventRules" });
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Audio Directory" };
            if (dialog.ShowDialog() == true)
            {
                _model.AudioPath = dialog.FolderName;
                _model.IsAnalyzed = false;
                _model.ProcessedEvents.Clear();
            }
        }

        private async void ProcessFolders_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_model.AudioPath) || !Directory.Exists(_model.AudioPath))
            {
                _logger.LogError("Invalid audio path selected.");
                return;
            }

            _model.IsProcessing = true;
            _model.ProcessedEvents.Clear();
            _model.FilteredProcessedEvents.Clear();
            _model.CharactersList.Clear();
            _model.CharactersList.Add("ALL");
            _model.SelectedCharacter = "ALL";
            _model.ProgressValue = 0;
            _logger.Logs.Clear();
            _logger.LogInfo($">>> ANALYZING: {_model.AudioPath}");

            string selectedLang = _model.SelectedLanguage ?? "EN";

            try {
                var reportProgress = new Action<double>(value => Application.Current.Dispatcher.Invoke(() => _model.ProgressValue = value));
                await Task.Run(async () => {
                    var allDirs = Directory.GetDirectories(_model.AudioPath, "*", SearchOption.AllDirectories)
                        .Concat(new[] { _model.AudioPath })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    int total = allDirs.Count;
                    int processedCount = 0;

                    foreach (var dir in allDirs) {
                        try {
                            if (dir.Contains("cast3D") || dir.Contains("cast2D")) continue;
                            var audioFiles = Directory.GetFiles(dir).Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".ogg")).ToList();
                            if (audioFiles.Count == 0) continue;

                            string folderName = Path.GetFileName(dir);
                            var parsedEvent = await _nameParser.ParseFolderNameAsync(folderName, selectedLang);
                            
                            string charName = "General";
                            var charMatch = Regex.Match(folderName, @"Play_vo_([A-Za-z0-9]+)(Skin\d+)?_");
                            if (charMatch.Success) charName = charMatch.Groups[1].Value;

                            string status = "Validated";
                            if (parsedEvent == null || string.IsNullOrEmpty(parsedEvent.DisplayText) || parsedEvent.DisplayText.Contains("event_") || parsedEvent.DisplayText.Contains("interaction_") || parsedEvent.DisplayText.Equals(folderName)) 
                                status = "Pending";
                            else if (parsedEvent.IconType != "generic" && string.IsNullOrEmpty(parsedEvent.IconPath)) 
                                status = "Missing Icon";

                            // Mark status optimistically; icons are resolved in the background after analysis
                            if (parsedEvent != null && parsedEvent.IconType == "generic")
                                status = "Ready";
                            else if (parsedEvent != null && !string.IsNullOrEmpty(parsedEvent.IconLookupName))
                                status = "Pending Icon";

                            string dialogueVal = _dialogueService.GetDialogue(selectedLang, folderName);

                            var ev = new PreviewEventModel { 
                                CharacterName = charName, 
                                FolderName = folderName, 
                                FolderPath = dir, 
                                ParsedData = parsedEvent ?? new ParsedEvent { OriginalFolder = folderName, DisplayText = folderName }, 
                                AudioFiles = audioFiles, 
                                Status = status,
                                Dialogue = dialogueVal
                            };
                            if (ev.ParsedData != null)
                            {
                                ev.ParsedData.Dialogue = dialogueVal;
                            }
                            
                            Application.Current.Dispatcher.Invoke(() => _model.ProcessedEvents.Add(ev));
                        } catch (Exception innerEx) {
                            _logger.LogError($"Failed to process folder: {Path.GetFileName(dir)} | Error: {innerEx.Message}");
                        }
                        
                        processedCount++;
                        reportProgress((double)processedCount / total * 100.0);
                    }
                    reportProgress(100.0);
                });
                _model.IsAnalyzed = _model.ProcessedEvents.Count > 0;
                if (_model.ProcessedEvents.Count > 0) _model.SelectedEvent = _model.ProcessedEvents[0];
                _logger.LogInfo($">>> ANALYSIS COMPLETE. Found {_model.ProcessedEvents.Count} events. Resolving icons in background...");
                // Keep 100% visible briefly before switching back to idle
                await Task.Delay(400);

                // Resolve icons concurrently in the background so the UI stays responsive
                _ = Task.Run(async () => await ResolvePendingIconsAsync());
            } catch (Exception ex) { 
                _logger.LogError("Critical analysis failure", ex); 
            }
            finally { _model.IsProcessing = false; }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.IsAnalyzed || _model.FilteredProcessedEvents.Count == 0) return;
            _model.IsProcessing = true;
            _logger.LogInfo(">>> STARTING BATCH RENDER...");
            try {
                var eventsToRender = _model.FilteredProcessedEvents.ToList();

                await Task.Run(async () => {
                    foreach (var ev in eventsToRender) {
                        // 1. Resolve pending icon in background safely
                        if (ev.ParsedData != null && ev.ParsedData.IconType != "generic" && string.IsNullOrEmpty(ev.ParsedData.IconPath))
                        {
                            string iconPath = await ResolveIconPathAsync(ev.ParsedData);
                            Application.Current.Dispatcher.Invoke(() => {
                                ev.ParsedData.IconPath = string.IsNullOrEmpty(iconPath) ? "MISSING" : iconPath;
                                ev.Status = string.IsNullOrEmpty(iconPath) ? "Missing Icon" : "Ready";
                            });
                        }

                        if (ev.Status == "No Audio" || ev.Status == "Missing Icon" || ev.ParsedData == null) continue;
                        
                        string dialogue = ev.Dialogue;
                        if (AppSettings.Instance.EnableTranscriptions && string.IsNullOrEmpty(dialogue) && ev.AudioFiles.Count > 0)
                        {
                            _logger.LogInfo($"Auto-transcribing on the fly for {ev.FolderName}...");
                            string transcription = await _transcriptionService.TranscribeAudiosAsync(ev.AudioFiles);
                            if (!string.IsNullOrEmpty(transcription))
                            {
                                dialogue = transcription;
                                ev.Dialogue = transcription;
                                if (ev.ParsedData != null)
                                {
                                    ev.ParsedData.Dialogue = transcription;
                                }
                                
                                string selectedLang = _model.SelectedLanguage ?? "EN";
                                _dialogueService.SetDialogue(selectedLang, ev.FolderName, transcription);
                                
                                // Update textbox in UI if this event is currently selected
                                if (_model.SelectedEvent == ev)
                                {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        QuickEditDialogue.Text = transcription;
                                    });
                                }
                            }
                        }

                        string tempImagePath = await _imageGenerator.CreateImageAsync(ev.ParsedData, AppSettings.Instance.SelectedFontName, AppSettings.Instance.CustomBackgroundPath, AppSettings.Instance.TextVerticalOffset);
                        if (string.IsNullOrEmpty(tempImagePath) || !File.Exists(tempImagePath))
                        {
                            tempImagePath = AppSettings.Instance.CustomBackgroundPath ?? AppConfig.BackgroundPath;
                        }

                        string outputPath = Path.Combine(AppConfig.OutputVideosDir, ev.FolderName + ".mp4");
                        await _videoService.CreateVideoAsync(tempImagePath, ev.AudioFiles, outputPath, 0.5, dialogue);
                    }
                });
                _logger.LogInfo(">>> BATCH PROCESS COMPLETED.");
            } catch (Exception ex) { _logger.LogError("Generation failed", ex); }
            finally { _model.IsProcessing = false; }
        }

        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null || _model.SelectedEvent.AudioFiles.Count == 0) return;
            var ev = _model.SelectedEvent;
            
            _logger.LogInfo($"Starting transcription for: {ev.FolderName}");
            _model.IsProcessing = true;
            try
            {
                if (ev.AudioFiles != null && ev.AudioFiles.Count > 0)
                {
                    string transcription = await _transcriptionService.TranscribeAudiosAsync(ev.AudioFiles);
                    if (!string.IsNullOrEmpty(transcription))
                    {
                        ev.Dialogue = transcription;
                        if (ev.ParsedData != null)
                        {
                            ev.ParsedData.Dialogue = transcription;
                        }
                        QuickEditDialogue.Text = transcription;
                        string selectedLang = _model.SelectedLanguage ?? "EN";
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
            catch (Exception ex)
            {
                _logger.LogError("Failed to auto-transcribe audio", ex);
            }
            finally
            {
                _model.IsProcessing = false;
            }
        }

        private bool _isDashboardMaximized = false;
        private GridLength _previousColumnWidth = new GridLength(380);
        private GridLength _prevHeaderHeight = GridLength.Auto;
        private GridLength _prevConfigHeight = GridLength.Auto;
        private GridLength _prevLogsHeight = GridLength.Auto;
        private GridLength _prevQuickEditHeight = GridLength.Auto;
        private GridLength _prevProgressHeight = GridLength.Auto;
        private Thickness _prevContainerMargin = new Thickness(12, 0, 0, 0);

        private void PreviewImage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                if (_model.SelectedEvent == null || _model.PreviewImageSource == null) return;

                _isDashboardMaximized = !_isDashboardMaximized;
                if (_isDashboardMaximized)
                {
                    // Save layouts
                    _previousColumnWidth = PipelineColumn.Width;
                    _prevHeaderHeight = HeaderRow.Height;
                    _prevConfigHeight = ConfigRow.Height;
                    _prevLogsHeight = LogsRow.Height;
                    _prevQuickEditHeight = QuickEditRow.Height;
                    _prevProgressHeight = ProgressRow.Height;
                    _prevContainerMargin = PreviewContainerGrid.Margin;

                    // Collapse all surrounding layouts to let the preview expand fully
                    PipelineColumn.MinWidth = 0;
                    PipelineColumn.Width = new GridLength(0);
                    SplitterColumn.Width = new GridLength(0);
                    HeaderRow.Height = new GridLength(0);
                    ConfigRow.Height = new GridLength(0);
                    LogsRow.Height = new GridLength(0);
                    QuickEditRow.Height = new GridLength(0);
                    ProgressRow.Height = new GridLength(0);
                    PreviewContainerGrid.Margin = new Thickness(0);
                }
                else
                {
                    // Restore layouts
                    PipelineColumn.MinWidth = 250;
                    PipelineColumn.Width = _previousColumnWidth.Value > 0 ? _previousColumnWidth : new GridLength(380);
                    SplitterColumn.Width = GridLength.Auto;
                    HeaderRow.Height = _prevHeaderHeight;
                    ConfigRow.Height = _prevConfigHeight;
                    LogsRow.Height = _prevLogsHeight;
                    QuickEditRow.Height = _prevQuickEditHeight;
                    ProgressRow.Height = _prevProgressHeight;
                    PreviewContainerGrid.Margin = _prevContainerMargin;
                }
            }
        }
    }
}
