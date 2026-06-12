using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoGenerator.Services
{
    public class AppSettings : ObservableObject
    {
        private static AppSettings _instance;
        private static readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "settings.json");

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
                    SaveSettings();
            }
        }

        private float _textVerticalOffset = -8f;
        public float TextVerticalOffset
        {
            get => _textVerticalOffset;
            set
            {
                if (SetProperty(ref _textVerticalOffset, value))
                    SaveSettings();
            }
        }

        private double _silenceDuration = 0.0;
        public double SilenceDuration
        {
            get => _silenceDuration;
            set
            {
                if (SetProperty(ref _silenceDuration, value))
                    SaveSettings();
            }
        }

        private string _defaultDictionaryLanguage = "ALL";
        public string DefaultDictionaryLanguage
        {
            get => _defaultDictionaryLanguage;
            set
            {
                if (SetProperty(ref _defaultDictionaryLanguage, value))
                    SaveSettings();
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

        private void SaveSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
    }
}
