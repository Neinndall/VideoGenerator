using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                // 1. Try to load from external file first
                if (File.Exists(_localTranslationsPath))
                {
                    string json = File.ReadAllText(_localTranslationsPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (data != null) 
                    {
                        _translations = data;
                        return;
                    }
                }

                // 2. If not found or invalid, load from embedded resource and save it out
                var uri = new Uri("pack://application:,,,/Resources/translations.json");
                var resourceStream = System.Windows.Application.GetResourceStream(uri);

                if (resourceStream != null)
                {
                    using var reader = new StreamReader(resourceStream.Stream);
                    string json = reader.ReadToEnd();
                    var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    
                    if (data != null) 
                    {
                        _translations = data;
                        // Ensure directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(_localTranslationsPath)!);
                        File.WriteAllText(_localTranslationsPath, json);
                    }
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
    }
}
