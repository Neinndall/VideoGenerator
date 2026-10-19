using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using VideoGenerator.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views
{
    public partial class SettingsView : UserControl
    {
        public List<double> SilenceOptions { get; } = new() { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };
        public List<string> LanguageOptions { get; } = new();
        public List<string> WhisperModelOptions { get; } = new() { "tiny", "base", "small", "medium" };
        public Dictionary<string, string> WhisperLanguageOptions { get; } = new()
        {
            { "Auto-Detect", "auto" },
            { "Turkish (Türkçe)", "tr" },
            { "English", "en" },
            { "Spanish (Español)", "es" },
            { "French (Français)", "fr" },
            { "German (Deutsch)", "de" },
            { "Portuguese (Português)", "pt" },
            { "Italian (Italiano)", "it" },
            { "Russian (Русский)", "ru" },
            { "Japanese (日本語)", "ja" },
            { "Korean (한국어)", "ko" },
            { "Chinese (中文)", "zh" }
        };

        public int MaxCpuThreads => System.Environment.ProcessorCount;

        public SettingsView(TranslationService translationService, LeagueWadAssetSource gameAssets)
        {
            // Load concrete dictionary languages for the application-wide language selector.
            LanguageOptions.Add("EN");
            try 
            {
                var raw = translationService.GetRawJson();
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(raw);
                if (data != null)
                {
                    foreach (var lang in data.Keys)
                    {
                        if (!lang.Equals("ALL", System.StringComparison.OrdinalIgnoreCase) &&
                            !LanguageOptions.Contains(lang, System.StringComparer.OrdinalIgnoreCase))
                        {
                            LanguageOptions.Add(lang);
                        }
                    }
                }
            } catch { }

            // Populate ordinary Lists before WPF binds to them; they do not
            // implement INotifyCollectionChanged when languages are appended.
            InitializeComponent();
            DataContext = this;
            GeneralPanel.SetActiveGameSource(gameAssets);
        }

        public AppSettings Settings => AppSettings.Instance;
    }
}
