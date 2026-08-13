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
    public class GroupManager
    {
        public ObservableCollection<ThematicGroup> Groups { get; } = new();
        private readonly string _configPath;
        private readonly LogService _logger;

        public GroupManager(LogService logger, string groupsFilePath = null)
        {
            _logger = logger;
            _configPath = string.IsNullOrWhiteSpace(groupsFilePath)
                ? AppConfig.GroupsPath
                : groupsFilePath;
            LoadGroups();
        }

        private void LoadGroups()
        {
            Groups.Clear();
            var defaults = DefaultGroups.Get();

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    var localGroups = JsonSerializer.Deserialize<List<ThematicGroup>>(json);
                    
                    if (localGroups != null)
                    {
                        // Deduplicate local groups by Name to prevent duplicate groups from getting loaded
                        localGroups = localGroups.GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

                        bool mergedAny = false;
                        foreach (var def in defaults)
                        {
                            var existing = localGroups.FirstOrDefault(g => g.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase));
                            if (existing == null)
                            {
                                localGroups.Add(def);
                                mergedAny = true;
                            }
                            else if (def.IsOfficial)
                            {
                                // Keep official group metadata in sync so regions/classes stay correctly categorized
                                if (!existing.Category.Equals(def.Category, StringComparison.OrdinalIgnoreCase))
                                {
                                    existing.Category = def.Category;
                                    mergedAny = true;
                                }
                                existing.IsOfficial = true;
                            }
                        }

                        // Remove legacy hardcoded skinlines so they are managed by SkinlineManager instead
                        localGroups.RemoveAll(g => g.IsOfficial && g.Category.Equals("Skinline", StringComparison.OrdinalIgnoreCase));

                        foreach (var g in localGroups.OrderBy(x => x.Category).ThenBy(x => x.Name))
                        {
                            Groups.Add(g);
                        }

                        if (mergedAny) SaveGroups();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load thematic groups. Default groups will be used.", ex);
                }
            }

            foreach (var g in defaults.OrderBy(x => x.Category).ThenBy(x => x.Name))
            {
                Groups.Add(g);
            }
            SaveGroups();
        }

        public void SaveGroups()
        {
            try
            {
                DirectoriesCreator.CreateParentDirectory(_configPath);
                string json = JsonSerializer.Serialize(Groups.ToList(), new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                File.WriteAllText(_configPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save thematic groups.", ex);
            }
        }
    }
}
