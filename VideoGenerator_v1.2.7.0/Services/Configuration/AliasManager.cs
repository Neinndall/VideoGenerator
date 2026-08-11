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
    public class AliasManager
    {
        private readonly string _configPath;
        private readonly LogService _logger;
        public ObservableCollection<ChampionAlias> Aliases { get; } = new();

        public AliasManager(LogService logger, string aliasesFilePath = null)
        {
            _logger = logger;
            _configPath = string.IsNullOrWhiteSpace(aliasesFilePath)
                ? AppConfig.Paths.AliasesPath
                : aliasesFilePath;
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
                    loaded = loaded.GroupBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load champion aliases. Default aliases will be used.", ex);
                }
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
                DirectoriesCreator.CreateParentDirectory(_configPath);
                string json = JsonSerializer.Serialize(Aliases.ToList(), new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                File.WriteAllText(_configPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save champion aliases.", ex);
            }
        }

        public string GetInternalName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return string.Empty;
            string clean = displayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            var alias = Aliases.FirstOrDefault(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                                     a.DisplayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "").Equals(clean, StringComparison.OrdinalIgnoreCase));
            
            return alias?.InternalName ?? clean;
        }

        private List<string> _validChampions;
        private readonly object _championsLock = new();

        public bool IsValidChampion(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            string clean = displayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            
            EnsureChampionsLoaded();
            if (_validChampions.Contains(clean, StringComparer.OrdinalIgnoreCase)) return true;

            return Aliases.Any(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                     a.DisplayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "").Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                                     a.InternalName.Equals(clean, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureChampionsLoaded()
        {
            if (_validChampions == null)
            {
                lock (_championsLock)
                {
                    if (_validChampions == null)
                    {
                        _validChampions = LoadChampionsList();
                    }
                }
            }
        }

        private List<string> LoadChampionsList()
        {
            var list = new List<string>();
            try
            {
                if (File.Exists(AppConfig.Paths.ChampionsPath))
                {
                    string json = File.ReadAllText(AppConfig.Paths.ChampionsPath);
                    var loaded = JsonSerializer.Deserialize<List<string>>(json);
                    if (loaded != null)
                    {
                        // Add normalized versions to the list for easy lookup
                        foreach (var champ in loaded)
                        {
                            list.Add(champ);
                            list.Add(champ.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", ""));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load the local champion database.", ex);
            }
            return list;
        }
    }
}
