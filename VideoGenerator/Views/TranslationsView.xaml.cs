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

        public TranslationsView(TranslationService translationService, RuleManager ruleManager)
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

            Loaded += (s, e) => 
            {
                LoadEntriesAsync();
                
                // Refresh suggestions on active load to ensure new mappings show up instantly
                _model.SuggestedEventKeys.Clear();
                var keys = ruleManager.Rules
                    .Select(r => r.TranslationKey)
                    .Where(k => !string.IsNullOrEmpty(k))
                    .Distinct()
                    .OrderBy(k => k);
                foreach (var key in keys)
                {
                    _model.SuggestedEventKeys.Add(key);
                }
            };
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
                _model.FilteredEntries.Remove(entry);
            }
        }

        private bool _isUpdatingText = false;
        private void KeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;

            // Only suggest when typing (adding characters)
            if (sender is System.Windows.Controls.TextBox textBox && e.Changes.Any(c => c.AddedLength > 0))
            {
                string currentText = textBox.Text;
                if (string.IsNullOrEmpty(currentText)) return;

                // Find a match that starts with the typed text
                string match = _model.SuggestedEventKeys
                    .FirstOrDefault(s => s.StartsWith(currentText, StringComparison.OrdinalIgnoreCase));

                if (match != null && match.Length > currentText.Length)
                {
                    _isUpdatingText = true;
                    int originalLength = currentText.Length;

                    // Set full suggested text
                    textBox.Text = currentText + match.Substring(originalLength);
                    
                    // Highlight (select) the suggested portion so typing continues naturally or is accepted
                    textBox.SelectionStart = originalLength;
                    textBox.SelectionLength = match.Length - originalLength;
                    _isUpdatingText = false;
                }
            }
        }

        private void KeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Accept the highlighted suggestion with Tab, Enter, or Right Arrow
                if (e.Key == System.Windows.Input.Key.Tab || e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Right)
                {
                    if (textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == textBox.Text.Length)
                    {
                        // Commit by moving caret to the end
                        textBox.SelectionStart = textBox.Text.Length;
                        textBox.SelectionLength = 0;
                        
                        // Prevent Tab from shifting focus if they just wanted to accept the suggestion
                        if (e.Key == System.Windows.Input.Key.Tab)
                        {
                            e.Handled = true;
                        }
                    }
                }
            }
        }
    }
}
