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

            _logger.LogInfo("Dashboard initialized.");
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
            }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_model.AudioPath) || !Directory.Exists(_model.AudioPath))
            {
                _logger.LogError("Invalid or empty audio path.");
                return;
            }

            _model.IsProcessing = true;
            _logger.Logs.Clear();
            _logger.LogInfo(">>> STARTING GENERATION PROCESS...");

            try
            {
                await Task.Run(async () =>
                {
                    string binFolder = GlobalFFOptions.Current.BinaryFolder;
                    if (string.IsNullOrEmpty(binFolder) || !Directory.Exists(binFolder))
                    {
                        _logger.LogError("FFmpeg binary folder is not configured or missing.");
                        return;
                    }

                    string ffmpegPath = Path.Combine(binFolder, "ffmpeg.exe");
                    if (!File.Exists(ffmpegPath))
                    {
                        _logger.LogError("ffmpeg.exe NOT FOUND.");
                        return;
                    }

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
                        _logger.LogInfo($">>>> PROCESSING: {charName}");

                        var eventFolders = Directory.GetDirectories(audioDir)
                            .Where(f => !f.Contains("cast3D") && !f.Contains("cast2D")).ToList();

                        if (eventFolders.Count == 0 && audioDir == _model.AudioPath) eventFolders.Add(audioDir);

                        foreach (var folderPath in eventFolders)
                        {
                            string folderName = Path.GetFileName(folderPath);
                            _logger.LogInfo($"- Parsing event: {folderName}");
                            
                            var parsedEvent = await _nameParser.ParseFolderNameAsync(folderName, _model.SelectedLanguage);
                            _logger.LogInfo($"  > Result: \"{parsedEvent.DisplayText}\" | Icon: {parsedEvent.IconLookupName} ({parsedEvent.IconType})");
                            
                            string iconPath = parsedEvent.IconType switch
                            {
                                "item" => await _iconManager.GetItemIconAsync(parsedEvent.IconLookupName),
                                "monster" => await _iconManager.GetMonsterIconAsync(parsedEvent.IconLookupName),
                                "champion" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                                _ => null
                            };
                            parsedEvent.IconPath = iconPath;

                            string imagePath = await _imageGenerator.CreateImageAsync(parsedEvent, _model.SelectedFontName, AppSettings.Instance.CustomBackgroundPath);
                            if (imagePath == null)
                            {
                                _logger.LogError($"Failed to create image for {folderName}");
                                continue;
                            }

                            var audioFiles = Directory.GetFiles(folderPath, "*.*")
                                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                                            f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(f => f).ToList();

                            if (audioFiles.Count > 0)
                            {
                                _logger.LogInfo($"  > Encoding video ({audioFiles.Count} audio files)...");
                                string outputVideoPath = Path.Combine(AppConfig.OutputVideosDir, charName, $"{folderName}.mp4");
                                await _videoService.CreateVideoAsync(imagePath, audioFiles, outputVideoPath, _model.SilenceDuration);
                            }
                            else
                            {
                                _logger.LogWarn($"No audio files found in {folderName}");
                            }
                        }
                    }
                });
                _logger.LogInfo(">>> PROCESS COMPLETED.");
            }
            catch (Exception ex) { _logger.LogError("Generation process failed", ex); }
            finally { _model.IsProcessing = false; }
        }
    }
}
