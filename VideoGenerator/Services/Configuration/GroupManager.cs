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
        private bool _canSave = true;
        private string _loadedRevision;

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
            _canSave = true;
            _loadedRevision = GetFileRevision();

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    var localGroups = JsonSerializer.Deserialize<List<ThematicGroup>>(json);
                    
                    if (localGroups == null || localGroups.Any(g => g == null ||
                        string.IsNullOrWhiteSpace(g.Name) ||
                        string.IsNullOrWhiteSpace(g.Category) || g.ChampionsRaw == null))
                        throw new JsonException("Invalid groups catalog.");

                    if (localGroups != null)
                    {
                        bool renamedAny = false;
                        foreach (var group in localGroups)
                        {
                            string trimmed = group.Name.Trim();
                            if (trimmed != group.Name) { group.Name = trimmed; renamedAny = true; }
                        }
                        // Deduplicate local groups by Name to prevent duplicate groups from getting loaded
                        int originalCount = localGroups.Count;
                        localGroups = localGroups.GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

                        bool mergedAny = localGroups.Count != originalCount || renamedAny;
                        // IsOfficial is derived from the shipped catalog, not
                        // a mutable boolean from arbitrary user JSON.
                        foreach (var group in localGroups)
                        {
                            bool official = defaults.Any(d => d.Name.Equals(
                                group.Name, StringComparison.OrdinalIgnoreCase));
                            if (group.IsOfficial != official) mergedAny = true;
                            group.IsOfficial = official;
                        }
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
                                if (!existing.IsOfficial) mergedAny = true;
                                existing.IsOfficial = true;
                            }
                        }

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
                    _logger.LogError("Failed to load thematic groups. The original groups.json is preserved; editing is disabled until it is repaired.", ex);
                    _canSave = false;
                }
            }

            foreach (var g in defaults.OrderBy(x => x.Category).ThenBy(x => x.Name))
            {
                Groups.Add(g);
            }
            if (_canSave) SaveGroups();
        }

        public bool SaveGroups()
        {
            if (!_canSave) return false;
            try
            {
                if (GetFileRevision() != _loadedRevision)
                {
                    _logger.LogWarn("groups.json changed elsewhere. Reopen Mapping to reload it before saving.");
                    return false;
                }
                DirectoriesCreator.CreateParentDirectory(_configPath);
                string json = JsonSerializer.Serialize(Groups.ToList(), new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                string temp = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temp, json, new UTF8Encoding(false));
                    File.Move(temp, _configPath, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                _loadedRevision = GetFileRevision();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save thematic groups.", ex);
                return false;
            }
        }

        private string GetFileRevision()
        {
            try
            {
                var file = new FileInfo(_configPath);
                return file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "<missing>";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return "<unavailable>";
            }
        }
    }
}
