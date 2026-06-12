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
            foreach (var lang in _translationService.AvailableLanguages)
                _model.AvailableLanguages.Add(lang);

            if (_model.AvailableLanguages.Count > 0)
                _model.SelectedLanguage = _model.AvailableLanguages.Contains("EN") ? "EN" : _model.AvailableLanguages[0];

            foreach (var family in SixLabors.Fonts.SystemFonts.Families)
                _model.FontNames.Add(family.Name);

            if (_model.FontNames.Count > 0)
                _model.SelectedFontName = _model.FontNames.Contains("Segoe UI") ? "Segoe UI" : _model.FontNames[0];

            _model.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(_model.SelectedEvent) && _model.SelectedEvent != null)
                {
                    await UpdatePreviewAsync();
                }
            };

            _logger.LogInfo("Dashboard initialized.");
        }

        private async Task UpdatePreviewAsync()
        {
            if (_model.SelectedEvent == null) return;

            try
            {
                string tempPreviewPath = await _imageGenerator.CreateImageAsync(
                    _model.SelectedEvent.ParsedData, 
                    _model.SelectedFontName, 
                    AppSettings.Instance.CustomBackgroundPath);
                
                _model.PreviewImagePath = tempPreviewPath;
            }
            catch (Exception ex)
            {
                _logger.LogError("Preview generation failed", ex);
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Audio Directory",
                InitialDirectory = _model.AudioPath ?? AppDomain.CurrentDomain.BaseDirectory
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                _model.AudioPath = dialog.FileName;
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
                    var audioDirs = Directory.GetDirectories(_model.AudioPath).ToList();

                    if (audioDirs.Count == 0)
                    {
                        var rootAudios = Directory.GetFiles(_model.AudioPath, "*.*")
                            .Where(s => s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                                        s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || 
                                        s.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                                        s.EndsWith(".wem", StringComparison.OrdinalIgnoreCase)).ToList();
                        
                        if (rootAudios.Count > 0) audioDirs.Add(_model.AudioPath);
                        else { _logger.LogError("No audio found."); return; }
                    }

                    foreach (var audioDir in audioDirs)
                    {
                        string charName = Path.GetFileName(audioDir);
                        var eventFolders = Directory.GetDirectories(audioDir)
                            .Where(f => !f.Contains("cast3D") && !f.Contains("cast2D")).ToList();

                        if (eventFolders.Count == 0 && audioDir == _model.AudioPath) eventFolders.Add(audioDir);

                        foreach (var folderPath in eventFolders)
                        {
                            string folderName = Path.GetFileName(folderPath);
                            var parsedEvent = await _nameParser.ParseFolderNameAsync(folderName, _model.SelectedLanguage);
                            
                            string iconPath = parsedEvent.IconType switch
                            {
                                "item" => await _iconManager.GetItemIconAsync(parsedEvent.IconLookupName),
                                "monster" => await _iconManager.GetMonsterIconAsync(parsedEvent.IconLookupName),
                                "champion" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
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
                            if (string.IsNullOrEmpty(iconPath)) status = "Missing Icon";
                            if (audioFiles.Count == 0) status = "No Audio";

                            Application.Current.Dispatcher.Invoke(() => {
                                _model.ProcessedEvents.Add(new PreviewEventModel {
                                    CharacterName = charName,
                                    FolderName = folderName,
                                    FolderPath = folderPath,
                                    ParsedData = parsedEvent,
                                    AudioFiles = audioFiles,
                                    Status = status
                                });
                            });
                        }
                    }
                });
                
                _model.IsAnalyzed = _model.ProcessedEvents.Count > 0;
                _logger.LogInfo($">>> ANALYSIS COMPLETE. Found {_model.ProcessedEvents.Count} events.");
                if (_model.ProcessedEvents.Count > 0) _model.SelectedEvent = _model.ProcessedEvents[0];
            }
            catch (Exception ex) { _logger.LogError("Analysis failed", ex); }
            finally { _model.IsProcessing = false; }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!_model.IsAnalyzed || _model.ProcessedEvents.Count == 0) return;

            _model.IsProcessing = true;
            _logger.LogInfo(">>> STARTING BATCH RENDER...");

            try
            {
                await Task.Run(async () =>
                {
                    string binFolder = GlobalFFOptions.Current.BinaryFolder;
                    if (string.IsNullOrEmpty(binFolder) || !Directory.Exists(binFolder))
                    {
                        _logger.LogError("FFmpeg binary folder not found.");
                        return;
                    }

                    foreach (var ev in _model.ProcessedEvents)
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
