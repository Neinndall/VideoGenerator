using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using FFMpegCore;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            
            DataContext = this; // Set DataContext to the View itself to access both Model and Logger
            DashboardModel = _model;
            LogService = _logger;

            InitializeData();
        }

        public DashboardModel DashboardModel { get; }
        public LogService LogService { get; }

        private void InitializeData()
        {
            _model.AudioPath = AppSettings.Instance.MediaSourceDirectory;

            // Sync with Settings when changed anywhere in the app
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

            _model.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(_model.SelectedEvent) && _model.SelectedEvent != null)
                {
                    await UpdatePreviewAsync();
                }
                else if (e.PropertyName == nameof(_model.SelectedFilter) || e.PropertyName == nameof(_model.SelectedCharacter))
                {
                    ApplyFilter();
                }
                else if (e.PropertyName == nameof(_model.SelectedFontName))
                {
                    AppSettings.Instance.SelectedFontName = _model.SelectedFontName;
                    await UpdatePreviewAsync();
                }
            };

            _model.ProcessedEvents.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    foreach (PreviewEventModel ev in e.NewItems)
                    {
                        // 1. Add character to characters list if not already present (no full rebuild)
                        if (!_model.CharactersList.Contains(ev.CharacterName))
                        {
                            // Keep character list sorted by inserting at the correct index
                            int insertIdx = 1; // Index 0 is "ALL"
                            while (insertIdx < _model.CharactersList.Count && string.Compare(_model.CharactersList[insertIdx], ev.CharacterName, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                insertIdx++;
                            }
                            _model.CharactersList.Insert(insertIdx, ev.CharacterName);
                        }

                        // 2. Direct filter check: add to FilteredProcessedEvents if matching (no full rebuild)
                        var filter = _model.SelectedFilter;
                        var characterFilter = _model.SelectedCharacter ?? "ALL";

                        if (characterFilter == "ALL" || string.Equals(ev.CharacterName, characterFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            bool include = filter switch
                            {
                                "ERRORS" => ev.Status == "Missing Icon" || ev.Status == "No Audio",
                                "PENDING" => ev.ParsedData == null || string.IsNullOrEmpty(ev.ParsedData.DisplayText) || ev.ParsedData.DisplayText.Contains("event_") || ev.ParsedData.DisplayText.Contains("interaction_") || ev.ParsedData.DisplayText.Equals(ev.FolderName),
                                _ => true
                            };

                            if (include)
                            {
                                _model.FilteredProcessedEvents.Add(ev);
                            }
                        }
                    }

                    // Automatically select first item if selection is empty
                    if (_model.SelectedEvent == null && _model.FilteredProcessedEvents.Count > 0)
                    {
                        _model.SelectedEvent = _model.FilteredProcessedEvents[0];
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (PreviewEventModel ev in e.OldItems)
                    {
                        _model.FilteredProcessedEvents.Remove(ev);

                        if (_model.SelectedEvent == ev)
                        {
                            _model.SelectedEvent = _model.FilteredProcessedEvents.FirstOrDefault();
                        }

                        bool hasOtherWithSameChar = _model.ProcessedEvents.Any(x => string.Equals(x.CharacterName, ev.CharacterName, StringComparison.OrdinalIgnoreCase));
                        if (!hasOtherWithSameChar && ev.CharacterName != "ALL")
                        {
                            _model.CharactersList.Remove(ev.CharacterName);
                            if (string.Equals(_model.SelectedCharacter, ev.CharacterName, StringComparison.OrdinalIgnoreCase))
                            {
                                _model.SelectedCharacter = "ALL";
                            }
                        }
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    _model.CharactersList.Clear();
                    _model.CharactersList.Add("ALL");
                    _model.FilteredProcessedEvents.Clear();
                }
            };

            _logger.LogInfo("Dashboard initialized.");
        }

        private void ApplyFilter()
        {
            var filter = _model.SelectedFilter;
            var characterFilter = _model.SelectedCharacter ?? "ALL";
            var currentSelected = _model.SelectedEvent;

            _model.FilteredProcessedEvents.Clear();
            foreach (var ev in _model.ProcessedEvents)
            {
                // Check character filter
                if (characterFilter != "ALL" && !string.Equals(ev.CharacterName, characterFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check status filter
                bool include = filter switch
                {
                    "ERRORS" => ev.Status == "Missing Icon" || ev.Status == "No Audio",
                    "PENDING" => ev.ParsedData == null || string.IsNullOrEmpty(ev.ParsedData.DisplayText) || ev.ParsedData.DisplayText.Contains("event_") || ev.ParsedData.DisplayText.Contains("interaction_") || ev.ParsedData.DisplayText.Equals(ev.FolderName),
                    _ => true
                };

                if (include)
                {
                    _model.FilteredProcessedEvents.Add(ev);
                }
            }

            if (currentSelected != null && _model.FilteredProcessedEvents.Contains(currentSelected))
            {
                _model.SelectedEvent = currentSelected;
            }
            else if (_model.FilteredProcessedEvents.Count > 0)
            {
                _model.SelectedEvent = _model.FilteredProcessedEvents[0];
            }
            else
            {
                _model.SelectedEvent = null;
            }
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true && rb.Tag != null)
            {
                _model.SelectedFilter = rb.Tag.ToString();
            }
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is PreviewEventModel ev)
            {
                _logger.LogInfo($"Removing event from pipeline: {ev.CharacterName} - {ev.FolderName}");
                _model.ProcessedEvents.Remove(ev);
            }
        }

        private async void ApplyQuickEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_model.SelectedEvent == null) return;

            var ev = _model.SelectedEvent;
            string key = ev.FolderName;
            string text = QuickEditDisplayText.Text;
            string iconName = QuickEditIconLookup.Text;
            string iconType = QuickEditIconType.SelectedValue?.ToString() ?? "generic";

            if (iconType == "item" && !string.IsNullOrEmpty(iconName))
            {
                string resolvedId = await _dataFetcher.ResolveItemNameToIdAsync(iconName);
                if (!string.IsNullOrEmpty(resolvedId))
                {
                    iconName = resolvedId;
                    QuickEditIconLookup.Text = resolvedId; // Update textbox in UI
                }
            }

            _logger.LogInfo($"Applying Quick Edit for folder: {key} -> '{text}' ({iconName}, {iconType})");

            // 1. Update translations.json global dictionary
            _translationService.UpdateTranslation(_model.SelectedLanguage, key, text);

            // 2. Refresh current in-memory parsed data
            ev.ParsedData.DisplayText = text;
            ev.ParsedData.IconLookupName = iconName;
            ev.ParsedData.IconType = iconType;

            // 3. Re-download or update local icon paths
            var lolVersion = await _dataFetcher.GetLatestLolVersionAsync();
            string iconPath = iconType switch
            {
                "item" => await _iconManager.GetItemIconAsync(iconName),
                "monster" => await _iconManager.GetMonsterIconAsync(iconName),
                "champion" => await _iconManager.GetChampionIconAsync(iconName, lolVersion),
                "structure" => await _iconManager.GetStructureIconAsync(iconName),
                _ => null
            };
            ev.ParsedData.IconPath = iconPath;

            // 4. Update status
            string newStatus = "Ready";
            if (string.IsNullOrEmpty(iconPath) && iconType != "generic") newStatus = "Missing Icon";
            if (ev.AudioFiles.Count == 0) newStatus = "No Audio";
            ev.Status = newStatus;

            // 5. Force update preview
            await UpdatePreviewAsync();

            // 6. Refresh filters in case of change
            ApplyFilter();
            _model.SelectedEvent = ev;
        }

        private async Task UpdatePreviewAsync()
        {
            if (_model.SelectedEvent == null) return;

            try
            {
                byte[] bytes = await _imageGenerator.CreateImageBytesAsync(
                    _model.SelectedEvent.ParsedData, 
                    _model.SelectedFontName, 
                    AppSettings.Instance.CustomBackgroundPath);
                
                if (bytes != null)
                {
                    Directory.CreateDirectory(AppConfig.CacheDir);
                    string tempPreviewPath = Path.Combine(AppConfig.CacheDir, "preview_temp.png");
                    await File.WriteAllBytesAsync(tempPreviewPath, bytes);
                    
                    _model.PreviewImagePath = null;
                    _model.PreviewImagePath = tempPreviewPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Preview generation failed", ex);
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Audio Directory",
                InitialDirectory = Directory.Exists(_model.AudioPath) ? _model.AudioPath : AppDomain.CurrentDomain.BaseDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                _model.AudioPath = dialog.FolderName;
                _logger.LogInfo($"Audio path selected: {_model.AudioPath}");
                _model.IsAnalyzed = false;
                _model.ProcessedEvents.Clear();
            }
        }

        private async void ProcessFolders_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_model.AudioPath) || !Directory.Exists(_model.AudioPath))
            {
                _logger.LogError("Invalid or empty media directory.");
                return;
            }

            _model.IsProcessing = true;
            _model.ProcessedEvents.Clear();
            _logger.Logs.Clear();
            _logger.LogInfo(">>> ANALYZING MEDIA DIRECTORY...");

            try
            {
                await Task.Run(async () =>
                {
                    var lolVersion = await _dataFetcher.GetLatestLolVersionAsync();
                    
                    // Recursive lookup: Find all directories that contain audio files directly
                    var allDirs = Directory.GetDirectories(_model.AudioPath, "*", SearchOption.AllDirectories).ToList();
                    
                    // Also include the selected folder itself in case it contains audio directly
                    allDirs.Insert(0, _model.AudioPath);

                    var eventFolders = new List<string>();
                    foreach (var dir in allDirs)
                    {
                        // Exclude cast folders and check if contains at least one audio file
                        if (dir.Contains("cast3D") || dir.Contains("cast2D")) continue;

                        bool hasAudio = Directory.GetFiles(dir, "*.*")
                            .Any(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                                      f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || 
                                      f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".wem", StringComparison.OrdinalIgnoreCase));
                        
                        if (hasAudio)
                        {
                            eventFolders.Add(dir);
                        }
                    }

                    if (eventFolders.Count == 0)
                    {
                        _logger.LogError("No folders containing audio files (.mp3, .wav, .ogg, .wem) were found.");
                        return;
                    }

                    foreach (var folderPath in eventFolders)
                    {
                        string folderName = Path.GetFileName(folderPath);
                        
                        // Extract Character Folder Name by walking up the directory tree
                        // until we reach the direct subdirectory of the selected root path.
                        string charFolderName = "General";
                        string current = folderPath;
                        string parent = Path.GetDirectoryName(current);
                        
                        while (!string.IsNullOrEmpty(parent))
                        {
                            if (parent.Equals(_model.AudioPath, StringComparison.OrdinalIgnoreCase))
                            {
                                charFolderName = Path.GetFileName(current);
                                break;
                            }
                            current = parent;
                            parent = Path.GetDirectoryName(current);
                        }

                        // If no subdirectory match was found (e.g. selected event folder itself), fallback to its own name or parent
                        if (charFolderName == "General" || string.IsNullOrEmpty(charFolderName))
                        {
                            charFolderName = Path.GetFileName(_model.AudioPath);
                        }

                        // Clean character folder name to extract clean champion name (e.g. "ahri_skin89_vo_audio" -> "Ahri")
                        string charName = charFolderName;
                        var cleanCharMatch = Regex.Match(charFolderName, @"^([A-Za-z0-9]+)_skin\d+", RegexOptions.IgnoreCase);
                        if (cleanCharMatch.Success)
                        {
                            charName = cleanCharMatch.Groups[1].Value;
                        }
                        else
                        {
                            // Strip suffixes like _vo_audio, _audio etc.
                            charName = Regex.Replace(charName, @"_vo_audio|_audio", "", RegexOptions.IgnoreCase);
                            charName = charName.Replace("_", " ");
                        }

                        // Capitalize first letter (e.g., ahri -> Ahri)
                        if (!string.IsNullOrEmpty(charName))
                        {
                            charName = charName.Substring(0, 1).ToUpper() + charName.Substring(1);
                        }

                        var parsedEvent = await _nameParser.ParseFolderNameAsync(folderName, _model.SelectedLanguage);
                        
                        string iconPath = parsedEvent.IconType switch
                        {
                            "item" => await _iconManager.GetItemIconAsync(parsedEvent.IconLookupName),
                            "monster" => await _iconManager.GetMonsterIconAsync(parsedEvent.IconLookupName),
                            "champion" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                            "structure" => await _iconManager.GetStructureIconAsync(parsedEvent.IconLookupName),
                            _ => null
                        };
                        parsedEvent.IconPath = iconPath;

                        var audioFiles = Directory.GetFiles(folderPath, "*.*")
                            .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(f => f).ToList();

                        string status = "Ready";
                        if (string.IsNullOrEmpty(iconPath) && parsedEvent.IconType != "generic") status = "Missing Icon";
                        if (audioFiles.Count == 0) status = "No Audio";

                        var ev = new PreviewEventModel {
                            CharacterName = charName,
                            FolderName = folderName,
                            FolderPath = folderPath,
                            ParsedData = parsedEvent,
                            AudioFiles = audioFiles,
                            Status = status
                        };

                        Application.Current.Dispatcher.Invoke(() => {
                            _model.ProcessedEvents.Add(ev);
                        });

                        await Task.Delay(25); // Allow UI thread to animate progress bar and render the new item smoothly
                    }
                });
                
                _model.IsAnalyzed = _model.ProcessedEvents.Count > 0;
                _logger.LogInfo($">>> ANALYSIS COMPLETE. Found {_model.ProcessedEvents.Count} folders containing audio.");
                if (_model.ProcessedEvents.Count > 0) _model.SelectedEvent = _model.ProcessedEvents[0];
            }
            catch (Exception ex) { _logger.LogError("Analysis failed", ex); }
            finally { _model.IsProcessing = false; }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.IsAnalyzed || _model.FilteredProcessedEvents.Count == 0) return;

            _model.IsProcessing = true;
            _logger.LogInfo(">>> STARTING BATCH RENDER...");

            try
            {
                // Create a local list of events to render from the filtered view
                var eventsToRender = _model.FilteredProcessedEvents.ToList();

                await Task.Run(async () =>
                {
                    string binFolder = GlobalFFOptions.Current.BinaryFolder;
                    if (string.IsNullOrEmpty(binFolder) || !Directory.Exists(binFolder))
                    {
                        _logger.LogError("FFmpeg binary folder not found.");
                        return;
                    }

                    foreach (var ev in eventsToRender)
                    {
                        if (ev.Status == "No Audio") continue;

                        _logger.LogInfo($"> Rendering: {ev.CharacterName} - {ev.FolderName}");
                        
                        string imagePath = await _imageGenerator.CreateImageAsync(ev.ParsedData, _model.SelectedFontName, AppSettings.Instance.CustomBackgroundPath);
                        if (imagePath == null) continue;

                        string outputVideoPath = Path.Combine(AppConfig.OutputVideosDir, ev.CharacterName, $"{ev.FolderName}.mp4");
                        await _videoService.CreateVideoAsync(imagePath, ev.AudioFiles, outputVideoPath, _model.SilenceDuration);
                    }
                });
                _logger.LogInfo(">>> BATCH PROCESS COMPLETED.");
            }
            catch (Exception ex) { _logger.LogError("Generation failed", ex); }
            finally { _model.IsProcessing = false; }
        }
    }
}
