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
    public class GroupManager
    {
        private readonly string _configPath;
        public ObservableCollection<ThematicGroup> Groups { get; } = new();

        public GroupManager()
        {
            _configPath = Path.Combine(AppConfig.ConfigDir, "thematic_groups.json");
            LoadGroups();
        }

        public void LoadGroups()
        {
            Groups.Clear();
            List<ThematicGroup> loadedGroups = new();

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    loadedGroups = JsonSerializer.Deserialize<List<ThematicGroup>>(json) ?? new();
                }
                catch { }
            }

            // Merge with Official Default Groups from Data Model
            var officialGroups = DefaultGroups.Get();
            foreach (var official in officialGroups)
            {
                var existing = loadedGroups.FirstOrDefault(g => g.Name.Equals(official.Name, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    official.IsOfficial = true;
                    loadedGroups.Add(official);
                }
                else
                {
                    existing.IsOfficial = true;
                }
            }

            foreach (var group in loadedGroups.OrderBy(g => g.Category).ThenBy(g => g.Name))
            {
                Groups.Add(group);
            }

            SaveGroups();
        }

        public void SaveGroups()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                string json = JsonSerializer.Serialize(Groups.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }
    }
}
