using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private readonly TranslationService _translationService;
        private readonly IconManager _iconManager;
        private readonly NameParser _nameParser;
        private readonly ImageGenerator _imageGenerator;
        private readonly VideoService _videoService;
        private readonly LogService _logger;

        public DashboardView(
            DataFetcher dataFetcher,
            TranslationService translationService,
            IconManager iconManager,
            NameParser nameParser,
            ImageGenerator imageGenerator,
            VideoService videoService,
            LogService logger)
        {
            InitializeComponent();
            _dataFetcher = dataFetcher;
            _translationService = translationService;
            _iconManager = iconManager;
            _nameParser = nameParser;
            _imageGenerator = imageGenerator;
            _videoService = videoService;
            _logger = logger;
            
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
                else if (e.PropertyName == nameof(AppSettings.CustomBackgroundPath))
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
            await ResolveEventIconAsync(_model.SelectedEvent);
            await GeneratePreviewAsync(_model.SelectedEvent);
        }

        private async Task GeneratePreviewAsync(PreviewEventModel ev)
        {
            try
            {
                if (ev.ParsedData == null) return;
                var bytes = await _imageGenerator.CreateImageBytesAsync(ev.ParsedData, "Arial", AppSettings.Instance.CustomBackgroundPath);
                if (bytes != null)
                {
                    Directory.CreateDirectory(AppConfig.CacheDir);
                    string tempPreviewPath = Path.Combine(AppConfig.CacheDir, "preview_temp.png");
                    await File.WriteAllBytesAsync(tempPreviewPath, bytes);
                    _model.PreviewImagePath = null;
                    _model.PreviewImagePath = tempPreviewPath;
                }
            }
            catch (Exception ex) { _logger.LogError("Preview generation failed", ex); }
        }

        private async Task ResolveEventIconAsync(PreviewEventModel ev)
        {
            if (ev?.ParsedData == null || ev.ParsedData.IconType == "generic" || !string.IsNullOrEmpty(ev.ParsedData.IconPath))
                return;

            string iconPath = await ResolveIconPathAsync(ev.ParsedData);
            ev.ParsedData.IconPath = iconPath;
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

                            var ev = new PreviewEventModel { 
                                CharacterName = charName, 
                                FolderName = folderName, 
                                FolderPath = dir, 
                                ParsedData = parsedEvent ?? new ParsedEvent { OriginalFolder = folderName, DisplayText = folderName }, 
                                AudioFiles = audioFiles, 
                                Status = status 
                            };
                            
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

                // Make sure every event has its icon resolved before rendering
                foreach (var ev in eventsToRender)
                    await ResolveEventIconAsync(ev);

                await Task.Run(async () => {
                    foreach (var ev in eventsToRender) {
                        if (ev.Status == "No Audio" || ev.Status == "Missing Icon" || ev.ParsedData == null) continue;
                        string outputPath = Path.Combine(AppConfig.OutputVideosDir, ev.FolderName + ".mp4");
                        await _videoService.CreateVideoAsync(ev.ParsedData.IconPath, ev.AudioFiles, outputPath, 0.5);
                    }
                });
                _logger.LogInfo(">>> BATCH PROCESS COMPLETED.");
            } catch (Exception ex) { _logger.LogError("Generation failed", ex); }
            finally { _model.IsProcessing = false; }
        }
    }
}
