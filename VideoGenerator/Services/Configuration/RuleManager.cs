using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class RuleManager
    {
        public ObservableCollection<EventRule> Rules { get; private set; } = new();
        private readonly string _rulesFilePath;
        private readonly LogService _logger;

        public RuleManager(LogService logger, string rulesFilePath = null)
        {
            _logger = logger;
            _rulesFilePath = string.IsNullOrWhiteSpace(rulesFilePath)
                ? AppConfig.EventRulesPath
                : rulesFilePath;
            LoadRules();
        }

        public void SaveRules()
        {
            try
            {
                string directory = Path.GetDirectoryName(_rulesFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    DirectoriesCreator.CreateDirectory(directory);
                }
                string json = JsonSerializer.Serialize(Rules, new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                File.WriteAllText(_rulesFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save event rules.", ex);
            }
        }

        private void LoadRules()
        {
            // 1. Get official base rules from Data Model
            var officialRules = DefaultRules.Get();
            
            if (File.Exists(_rulesFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_rulesFilePath);
                    var userRules = JsonSerializer.Deserialize<List<EventRule>>(json);
                    
                    if (userRules != null)
                    {
                        // Deduplicate user rules by Keyword to clean up any past duplicates (like Death)
                        userRules = userRules.GroupBy(r => r.Keyword, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

                        // User-saved rules are authoritative for existing
                        // keywords. Mapping edits (including overrides of
                        // built-in rules) must survive an app upgrade/restart.
                        // Only append genuinely new official keywords.
                        // A future provenance-aware migration may update
                        // untouched defaults separately from user overrides.
                        var mergedList = new List<EventRule>(userRules);
                        
                        foreach (var official in officialRules)
                        {
                            var existing = mergedList.FirstOrDefault(r => r.Keyword.Equals(official.Keyword, StringComparison.OrdinalIgnoreCase));
                            if (existing == null)
                            {
                                mergedList.Add(official);
                            }
                        }
                        
                        Rules = new ObservableCollection<EventRule>(mergedList.OrderBy(r => r.Keyword));
                        SaveRules();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Preserve a corrupt/unreadable user file intact so it
                    // can be repaired or restored. Do not silently replace it
                    // with default rules on the next startup.
                    _logger.LogError("Failed to load event rules. Using defaults in memory; the existing user file was preserved.", ex);
                    Rules = new ObservableCollection<EventRule>(officialRules.OrderBy(r => r.Keyword));
                    return;
                }
            }

            // Fallback: Official rules only
            Rules = new ObservableCollection<EventRule>(officialRules.OrderBy(r => r.Keyword));
            SaveRules();
        }
    }
}
