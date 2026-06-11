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
    public class AliasManager
    {
        private readonly string _configPath;
        public ObservableCollection<ChampionAlias> Aliases { get; } = new();

        public AliasManager()
        {
            _configPath = Path.Combine(AppConfig.ConfigDir, "champion_aliases.json");
            LoadAliases();
        }

        public void LoadAliases()
        {
            Aliases.Clear();
            List<ChampionAlias> loaded = new();

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    loaded = JsonSerializer.Deserialize<List<ChampionAlias>>(json) ?? new();
                }
                catch { }
            }

            var defaults = DefaultAliases.Get();
            foreach (var def in defaults)
            {
                var existing = loaded.FirstOrDefault(a => a.DisplayName.Equals(def.DisplayName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    def.IsOfficial = true;
                    loaded.Add(def);
                }
                else
                {
                    existing.IsOfficial = true;
                }
            }

            foreach (var alias in loaded.OrderBy(a => a.DisplayName))
            {
                Aliases.Add(alias);
            }

            SaveAliases();
        }

        public void SaveAliases()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                string json = JsonSerializer.Serialize(Aliases.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public string GetInternalName(string displayName)
        {
            string clean = displayName.Replace(" ", "").Replace("'", "");
            var alias = Aliases.FirstOrDefault(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                                     a.DisplayName.Replace(" ", "").Equals(clean, StringComparison.OrdinalIgnoreCase));
            
            return alias?.InternalName ?? clean;
        }
    }
}
