using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
                string json = JsonSerializer.Serialize(Rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_rulesFilePath, json);
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
                        // 2. Merge Strategy: Add official rules that are missing
                        var mergedList = new List<EventRule>(userRules);
                        
                        foreach (var official in officialRules)
                        {
                            if (!mergedList.Any(r => r.Keyword.Equals(official.Keyword, StringComparison.OrdinalIgnoreCase)))
                            {
                                mergedList.Add(official);
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
