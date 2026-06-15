using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    public class TranslationService
    {
        private Dictionary<string, Dictionary<string, string>> _translations = new();
        private readonly string _localTranslationsPath;

        public IEnumerable<string> AvailableLanguages => _translations.Keys;

        public TranslationService()
        {
            _localTranslationsPath = AppConfig.TranslationsPath; // Points to an external path like Config/translations.json
            LoadTranslations();
        }

        public string GetRawJson()
        {
            if (File.Exists(_localTranslationsPath))
            {
                return File.ReadAllText(_localTranslationsPath);
            }
            return "{}";
        }

        public void SaveRawJson(string jsonContent)
        {
            // Validate JSON before saving
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);
            if (data != null)
            {
                File.WriteAllText(_localTranslationsPath, jsonContent);
                _translations = data;
            }
        }

        private void LoadTranslations()
        {
            try
            {
                // 1. Load from embedded resource first (acts as default / fallback source)
                Dictionary<string, Dictionary<string, string>> embeddedData = new();
                var uri = new Uri("pack://application:,,,/Resources/translations.json");
                var resourceStream = System.Windows.Application.GetResourceStream(uri);

                if (resourceStream != null)
                {
                    using var reader = new StreamReader(resourceStream.Stream);
                    string json = reader.ReadToEnd();
                    var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (data != null)
                    {
                        embeddedData = data;
                    }
                }

                // 2. Try to load from external file
                if (File.Exists(_localTranslationsPath))
                {
                    string json = File.ReadAllText(_localTranslationsPath);
                    var localData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (localData != null)
                    {
                        bool mergedAny = false;
                        // Merge embedded keys that are missing in the local file
                        foreach (var langPair in embeddedData)
                        {
                            string lang = langPair.Key;
                            if (!localData.ContainsKey(lang))
                            {
                                localData[lang] = new Dictionary<string, string>();
                                mergedAny = true;
                            }

                            foreach (var keyPair in langPair.Value)
                            {
                                if (!localData[lang].ContainsKey(keyPair.Key))
                                {
                                    localData[lang][keyPair.Key] = keyPair.Value;
                                    mergedAny = true;
                                }
                            }
                        }

                        _translations = localData;

                        if (mergedAny)
                        {
                            // Save updated local translations back
                            string updatedJson = JsonSerializer.Serialize(_translations, new JsonSerializerOptions 
                            { 
                                WriteIndented = true,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                            File.WriteAllText(_localTranslationsPath, updatedJson);
                        }
                        return;
                    }
                }

                // 3. If no external file exists, use embedded and save it to AppData
                _translations = embeddedData;
                if (_translations.Count > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_localTranslationsPath)!);
                    string json = JsonSerializer.Serialize(_translations, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(_localTranslationsPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading translations: {ex.Message}");
            }
        }

        public string GetText(string language, string key, params object[] args)
        {
            if (!_translations.ContainsKey(language) || !_translations[language].ContainsKey(key))
            {
                return key;
            }

            string text = _translations[language][key];
            
            if (args != null && args.Length > 0)
            {
                try
                {
                    if (text.Contains("{") && !Regex.IsMatch(text, @"\{\d+\}"))
                    {
                        text = Regex.Replace(text, @"\{[a-zA-Z0-9_]+\}", args[0].ToString() ?? "");
                        return text;
                    }
                    return string.Format(text, args);
                }
                catch (Exception)
                {
                    return text;
                }
            }
            
            return text;
        }

        public string GetText(string language, string key, Dictionary<string, string> placeholders)
        {
            if (!_translations.ContainsKey(language) || !_translations[language].ContainsKey(key))
                return key;

            string text = _translations[language][key];
            foreach (var kvp in placeholders)
            {
                text = text.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
            return text;
        }
        public void UpdateTranslation(string language, string key, string value)
        {
            if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(key)) return;

            if (!_translations.ContainsKey(language))
            {
                _translations[language] = new Dictionary<string, string>();
            }

            _translations[language][key] = value;

            try
            {
                string json = JsonSerializer.Serialize(_translations, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(_localTranslationsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving updated translations: {ex.Message}");
            }
        }
    }
}
