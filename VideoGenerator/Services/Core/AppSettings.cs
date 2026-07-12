using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Threading;
using VideoGenerator.Utils;

namespace VideoGenerator.Services
{
    public class AppSettings : ObservableObject
    {
        private static AppSettings _instance;
        private static readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGenerator", "Config", "settings.json");

        private static Timer _saveTimer;
        private static readonly object _saveLock = new();

        [JsonIgnore]
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }
                return _instance;
            }
        }

        private string _customBackgroundPath;
        public string CustomBackgroundPath
        {
            get => _customBackgroundPath;
            set
            {
                if (SetProperty(ref _customBackgroundPath, value))
                    QueueSave();
            }
        }

        private float _backgroundBrightness = 1.0f;
        public float BackgroundBrightness
        {
            get => _backgroundBrightness;
            set
            {
                if (SetProperty(ref _backgroundBrightness, Math.Clamp(value, 0f, 2f)))
                    QueueSave();
            }
        }

        private float _backgroundContrast = 1.0f;
        public float BackgroundContrast
        {
            get => _backgroundContrast;
            set
            {
                if (SetProperty(ref _backgroundContrast, Math.Clamp(value, 0f, 2f)))
                    QueueSave();
            }
        }

        private float _backgroundSaturate = 1.0f;
        public float BackgroundSaturate
        {
            get => _backgroundSaturate;
            set
            {
                if (SetProperty(ref _backgroundSaturate, Math.Clamp(value, 0f, 2f)))
                    QueueSave();
            }
        }

        private string _mediaSourceDirectory;
        public string MediaSourceDirectory
        {
            get => _mediaSourceDirectory;
            set
            {
                if (SetProperty(ref _mediaSourceDirectory, value))
                    QueueSave();
            }
        }

        private float _textVerticalOffset = -8f;
        public float TextVerticalOffset
        {
            get => _textVerticalOffset;
            set
            {
                if (SetProperty(ref _textVerticalOffset, value))
                    QueueSave();
            }
        }

        private string _iconAlignment = "Left";
        public string IconAlignment
        {
            get => _iconAlignment;
            set
            {
                if (SetProperty(ref _iconAlignment, value))
                    QueueSave();
            }
        }

        private float _iconVerticalOffset = 0f;
        public float IconVerticalOffset
        {
            get => _iconVerticalOffset;
            set
            {
                if (SetProperty(ref _iconVerticalOffset, value))
                    QueueSave();
            }
        }

        private string _selectedFontName = "Segoe UI";
        public string SelectedFontName
        {
            get => _selectedFontName;
            set
            {
                if (SetProperty(ref _selectedFontName, value))
                    QueueSave();
            }
        }

        private double _silenceDuration = 0.0;
        public double SilenceDuration
        {
            get => _silenceDuration;
            set
            {
                if (SetProperty(ref _silenceDuration, Math.Clamp(value, 0d, 10d)))
                    QueueSave();
            }
        }

        private bool _mergeAudioFamilies = false;
        public bool MergeAudioFamilies
        {
            get => _mergeAudioFamilies;
            set
            {
                if (SetProperty(ref _mergeAudioFamilies, value))
                    QueueSave();
            }
        }

        private string _defaultDictionaryLanguage = "ALL";
        public string DefaultDictionaryLanguage
        {
            get => _defaultDictionaryLanguage;
            set
            {
                if (SetProperty(ref _defaultDictionaryLanguage, value))
                    QueueSave();
            }
        }

        private bool _enableTranscriptions = true;
        public bool EnableTranscriptions
        {
            get => _enableTranscriptions;
            set
            {
                if (SetProperty(ref _enableTranscriptions, value))
                    QueueSave();
            }
        }

        private string _whisperLanguage = "auto";
        public string WhisperLanguage
        {
            get => _whisperLanguage;
            set
            {
                if (SetProperty(ref _whisperLanguage, value))
                    QueueSave();
            }
        }

        private string _whisperModel = "base";
        public string WhisperModel
        {
            get => _whisperModel;
            set
            {
                if (SetProperty(ref _whisperModel, value))
                    QueueSave();
            }
        }

        private float _bubbleTextSize = 22f;
        public float BubbleTextSize
        {
            get => _bubbleTextSize;
            set
            {
                if (SetProperty(ref _bubbleTextSize, Math.Clamp(value, 8f, 72f)))
                    QueueSave();
            }
        }

        private float _bubbleHeight = 120f;
        public float BubbleHeight
        {
            get => _bubbleHeight;
            set
            {
                if (SetProperty(ref _bubbleHeight, Math.Clamp(value, 60f, 360f)))
                    QueueSave();
            }
        }

        private float _bubbleOpacity = 0.85f;
        public float BubbleOpacity
        {
            get => _bubbleOpacity;
            set
            {
                if (SetProperty(ref _bubbleOpacity, Math.Clamp(value, 0f, 1f)))
                    QueueSave();
            }
        }

        private float _bubbleVerticalOffset = 0f;
        public float BubbleVerticalOffset
        {
            get => _bubbleVerticalOffset;
            set
            {
                if (SetProperty(ref _bubbleVerticalOffset, value))
                    QueueSave();
            }
        }

        private float _bubbleWidth = 900f;
        public float BubbleWidth
        {
            get => _bubbleWidth;
            set
            {
                if (SetProperty(ref _bubbleWidth, Math.Clamp(value, 320f, 1400f)))
                    QueueSave();
            }
        }

        private float _bubbleHorizontalOffset = 0f;
        public float BubbleHorizontalOffset
        {
            get => _bubbleHorizontalOffset;
            set
            {
                if (SetProperty(ref _bubbleHorizontalOffset, value))
                    QueueSave();
            }
        }

        private string NormalizeColorInput(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return val;
            var match = System.Text.RegularExpressions.Regex.Match(val, @"#[0-9A-Fa-f]{3,8}");
            if (match.Success)
            {
                return match.Value;
            }
            string lower = val.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "white": return "#FFFFFF";
                case "gold": return "#C89B3C";
                case "hextech gold": return "#C89B3C";
            }
            return val;
        }

        private string _bubbleBorderColor = "#C89B3C";
        public string BubbleBorderColor
        {
            get => _bubbleBorderColor;
            set
            {
                string normalized = NormalizeColorInput(value);
                if (SetProperty(ref _bubbleBorderColor, normalized))
                    QueueSave();
            }
        }

        private string _iconBorderColor = "#C89B3C";
        public string IconBorderColor
        {
            get => _iconBorderColor;
            set
            {
                string normalized = NormalizeColorInput(value);
                if (SetProperty(ref _iconBorderColor, normalized))
                    QueueSave();
            }
        }

        private float _iconBorderThickness = 2f;
        public float IconBorderThickness
        {
            get => _iconBorderThickness;
            set
            {
                if (SetProperty(ref _iconBorderThickness, Math.Clamp(value, 0f, 5f)))
                    QueueSave();
            }
        }

        private float _bubbleBorderThickness = 2f;
        public float BubbleBorderThickness
        {
            get => _bubbleBorderThickness;
            set
            {
                if (SetProperty(ref _bubbleBorderThickness, Math.Clamp(value, 0f, 5f)))
                    QueueSave();
            }
        }

        private bool _cleanWhisperHallucinations = false;
        public bool CleanWhisperHallucinations
        {
            get => _cleanWhisperHallucinations;
            set
            {
                if (SetProperty(ref _cleanWhisperHallucinations, value))
                    QueueSave();
            }
        }

        private bool _forceBatchRetranscribe = false;
        public bool ForceBatchRetranscribe
        {
            get => _forceBatchRetranscribe;
            set
            {
                if (SetProperty(ref _forceBatchRetranscribe, value))
                    QueueSave();
            }
        }

        private int _whisperThreadCount = Math.Max(1, Environment.ProcessorCount / 2);
        public int WhisperThreadCount
        {
            get => _whisperThreadCount;
            set
            {
                // Clamp between 1 and Environment.ProcessorCount
                int clamped = Math.Clamp(value, 1, Environment.ProcessorCount);
                if (SetProperty(ref _whisperThreadCount, clamped))
                    QueueSave();
            }
        }

        [JsonConstructor]
        public AppSettings() { }

        private static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new AppSettings();
        }

        private void QueueSave()
        {
            lock (_saveLock)
            {
                if (_saveTimer == null)
                {
                    _saveTimer = new Timer(SaveCallback, null, 500, Timeout.Infinite);
                }
                else
                {
                    _saveTimer.Change(500, Timeout.Infinite);
                }
            }
        }

        private void SaveCallback(object state)
        {
            SaveSettings();
        }

        public void SaveSettings()
        {
            lock (_saveLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_settingsPath);
                    DirectoriesCreator.CreateDirectory(dir);

                    string json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    string temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
                    try
                    {
                        File.WriteAllText(temporaryPath, json, Encoding.UTF8);
                        File.Move(temporaryPath, _settingsPath, true);
                    }
                    finally
                    {
                        try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                    }
                }
                catch { }
            }
        }
    }
}
