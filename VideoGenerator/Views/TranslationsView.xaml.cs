using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views
{
    public partial class TranslationsView : UserControl
    {
        private readonly TranslationsModel _model = new();
        private readonly TranslationService _translationService;

        public TranslationsView(TranslationService translationService)
        {
            InitializeComponent();
            _translationService = translationService;
            DataContext = _model;

            // Pre-set language to avoid UI flicker while loading
            _model.SelectedLanguage = AppSettings.Instance.DefaultDictionaryLanguage;

            _model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_model.SelectedLanguage) || e.PropertyName == nameof(_model.SearchQuery))
                {
                    ApplyFilter();
                }
            };

            Loaded += (s, e) => LoadEntriesAsync();
        }

        private async void LoadEntriesAsync()
        {
            if (_model.AllEntries.Count > 0) return;

            try
            {
                _model.IsLoading = true;
                _model.StatusMessage = "Loading dictionary...";
                
                // Ensure SelectedLanguage is set before loading starts
                _model.SelectedLanguage = AppSettings.Instance.DefaultDictionaryLanguage;

                await Task.Delay(50);

                var data = await Task.Run(() => {
                    string rawJson = _translationService.GetRawJson();
                    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(rawJson);
                });
                
                _model.AllEntries.Clear();
                _model.AvailableLanguages.Clear();
                _model.AvailableLanguages.Add("ALL");

                if (data != null)
                {
                    var tempEntries = new List<TranslationEntry>();
                    foreach (var langPair in data)
                    {
                        if (!_model.AvailableLanguages.Contains(langPair.Key))
                            _model.AvailableLanguages.Add(langPair.Key);

                        foreach (var keyPair in langPair.Value)
                        {
                            tempEntries.Add(new TranslationEntry
                            {
                                Language = langPair.Key,
                                Key = keyPair.Key,
                                Value = keyPair.Value
                            });
                        }
                    }

                    int batchCount = 0;
                    foreach (var entry in tempEntries)
                    {
                        _model.AllEntries.Add(entry);
                        if (++batchCount % 100 == 0) await Task.Delay(1);
                    }
                }
                
                // Load default language from settings
                string defaultLang = AppSettings.Instance.DefaultDictionaryLanguage;
                if (_model.AvailableLanguages.Contains(defaultLang))
                    _model.SelectedLanguage = defaultLang;
                else
                    _model.SelectedLanguage = "ALL";

                ApplyFilter();
                _model.StatusMessage = $"✓ Loaded {_model.AllEntries.Count} entries.";
            }
            catch (Exception ex)
            {
                _model.StatusMessage = $"Failed to load: {ex.Message}";
            }
            finally
            {
                _model.IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var query = _model.SearchQuery?.ToLower() ?? "";
            var lang = _model.SelectedLanguage;

            var filtered = _model.AllEntries.Where(e => 
                (lang == "ALL" || e.Language == lang) &&
                (string.IsNullOrEmpty(query) || e.Key.ToLower().Contains(query) || e.Value.ToLower().Contains(query))
            ).ToList();

            _model.FilteredEntries.Clear();
            foreach (var entry in filtered)
                _model.FilteredEntries.Add(entry);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dict = new Dictionary<string, Dictionary<string, string>>();
                var seenKeys = new HashSet<string>();
                
                foreach (var entry in _model.AllEntries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Language) || string.IsNullOrWhiteSpace(entry.Key))
                        continue;

                    string comboKey = $"{entry.Language.ToUpper()}_{entry.Key.ToLower()}";
                    if (seenKeys.Contains(comboKey))
                    {
                        MessageBox.Show($"Duplicate entry detected: The key '{entry.Key}' is defined multiple times for language '{entry.Language}'.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    seenKeys.Add(comboKey);

                    if (!dict.ContainsKey(entry.Language))
                        dict[entry.Language] = new Dictionary<string, string>();
                    
                    dict[entry.Language][entry.Key] = entry.Value ?? "";
                }

                string newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                _translationService.SaveRawJson(newJson);
                _model.StatusMessage = "✓ Dictionary saved successfully.";
            }
            catch (Exception ex)
            {
                _model.StatusMessage = $"✗ Save failed: {ex.Message}";
            }
        }

        private void AddNew_Click(object sender, RoutedEventArgs e)
        {
            string initialLang = _model.SelectedLanguage == "ALL" ? "EN" : _model.SelectedLanguage;
            var newEntry = new TranslationEntry { Language = initialLang, Key = "new_event", Value = "New Text" };
            _model.AllEntries.Insert(0, newEntry);
            ApplyFilter();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TranslationEntry entry)
            {
                _model.AllEntries.Remove(entry);
                ApplyFilter();
            }
        }
    }
}
