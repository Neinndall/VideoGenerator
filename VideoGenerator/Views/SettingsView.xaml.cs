using System.Collections.Generic;
using System.Windows.Controls;
using VideoGenerator.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views
{
    public partial class SettingsView : UserControl
    {
        public List<double> SilenceOptions { get; } = new() { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };
        public List<float> OffsetOptions { get; } = new() { -20f, -15f, -10f, -8f, -5f, 0f, 5f, 10f, 20f };
        public List<string> LanguageOptions { get; } = new();

        public SettingsView(TranslationService translationService)
        {
            InitializeComponent();
            DataContext = this;

            // Load available languages for the dropdown
            LanguageOptions.Add("ALL");
            try 
            {
                var raw = translationService.GetRawJson();
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(raw);
                if (data != null)
                {
                    foreach (var lang in data.Keys)
                        LanguageOptions.Add(lang);
                }
            } catch { }
        }

        public AppSettings Settings => AppSettings.Instance;
    }
}
