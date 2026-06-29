using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class RuleManager
    {
        public ObservableCollection<EventRule> Rules { get; private set; } = new();
        private readonly string _rulesFilePath;

        public RuleManager()
        {
            _rulesFilePath = Path.Combine(AppConfig.ConfigDir, "event_rules.json");
            LoadRules();
        }

        public void SaveRules()
        {
            try
            {
                Directory.CreateDirectory(AppConfig.ConfigDir);
                string json = JsonSerializer.Serialize(Rules, new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                File.WriteAllText(_rulesFilePath, json, Encoding.UTF8);
            }
            catch { }
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

                        // 2. Merge Strategy: Add official rules that are missing or update them if changed
                        var mergedList = new List<EventRule>(userRules);
                        
                        foreach (var official in officialRules)
                        {
                            var existing = mergedList.FirstOrDefault(r => r.Keyword.Equals(official.Keyword, StringComparison.OrdinalIgnoreCase));
                            if (existing == null)
                            {
                                mergedList.Add(official);
                            }
                            else
                            {
                                // Overwrite system properties to sync code updates for default rules
                                existing.TranslationKey = official.TranslationKey;
                                existing.IconType = official.IconType;
                                existing.IconLookup = official.IconLookup;
                                existing.Section = official.Section;
                                existing.Type = official.Type;
                                existing.ExtractsTarget = official.ExtractsTarget;
                            }
                        }
                        
                        Rules = new ObservableCollection<EventRule>(mergedList.OrderBy(r => r.Keyword));
                        SaveRules();
                        return;
                    }
                }
                catch { }
            }

            // Fallback: Official rules only
            Rules = new ObservableCollection<EventRule>(officialRules.OrderBy(r => r.Keyword));
            SaveRules();
        }
    }
}
