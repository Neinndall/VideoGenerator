using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text;
using VideoGenerator.Models;
using VideoGenerator.Utils;

namespace VideoGenerator.Services
{
    public class DialogueService : IDialogueStore
    {
        private Dictionary<string, Dictionary<string, string>> _dialogues = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _filePath;
        private readonly object _lock = new();
        private readonly LogService _logger;

        public DialogueService(LogService logger)
        {
            _logger = logger;
            _filePath = AppConfig.Paths.DialoguesPath;
            LoadDialogues();
        }

        public static string CleanDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"\[.*?\]", "").Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        public string GetDialogue(string language, string folderName)
        {
            lock (_lock)
            {
                if (_dialogues.TryGetValue(language, out var langDict))
                {
                    if (langDict.TryGetValue(folderName, out var text))
                    {
                        return text;
                    }
                }
                return "";
            }
        }

        public void SetDialogue(string language, string folderName, string text)
        {
            lock (_lock)
            {
                if (!_dialogues.ContainsKey(language))
                {
                    _dialogues[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                _dialogues[language][folderName] = text;
                SaveDialogues();
            }
        }

        public bool DialogueExists(string language, string folderName)
        {
            lock (_lock)
            {
                if (_dialogues.TryGetValue(language, out var langDict))
                {
                    return langDict.ContainsKey(folderName);
                }
                return false;
            }
        }

        private void LoadDialogues()
        {
            try
            {
                bool migrated = false;
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (data != null)
                    {
                        var normalized = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in data)
                        {
                            normalized[kvp.Key] = new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
                        }
                        _dialogues = normalized;
                        return;
                    }
                }

                // Migrate from translations.json if it exists and dialogues.json is empty
                if (File.Exists(AppConfig.Paths.TranslationsPath))
                {
                    string transJson = File.ReadAllText(AppConfig.Paths.TranslationsPath);
                    var transData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(transJson);
                    if (transData != null)
                    {
                        var migratedDict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var langPair in transData)
                        {
                            string lang = langPair.Key;
                            var langDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var keyPair in langPair.Value)
                            {
                                if (keyPair.Key.StartsWith("dialogue_", StringComparison.OrdinalIgnoreCase))
                                {
                                    string folderName = keyPair.Key.Substring("dialogue_".Length);
                                    langDict[folderName] = keyPair.Value;
                                    migrated = true;
                                }
                            }
                            if (langDict.Count > 0)
                            {
                                migratedDict[lang] = langDict;
                            }
                        }

                        if (migrated)
                        {
                            _dialogues = migratedDict;
                            SaveDialogues();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load or migrate dialogues. An empty dialogue database will be used.", ex);
            }

            if (_dialogues == null)
            {
                _dialogues = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveDialogues()
        {
            try
            {
                DirectoriesCreator.CreateParentDirectory(_filePath);
                string json = JsonSerializer.Serialize(_dialogues, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(_filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save dialogues.", ex);
            }
        }
    }
}
