using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Centralized authority for League of Legends skinlines.
    /// Loads thematic skin data from CommunityDragon and provides fast lookups.
    /// </summary>
    public class SkinlineManager
    {
        private readonly DataFetcher _dataFetcher;
        private readonly AliasManager _aliasManager;

        private SkinlineCatalog _catalog;
        private bool _loaded;
        private readonly object _loadLock = new();

        public SkinlineManager(DataFetcher dataFetcher, AliasManager aliasManager)
        {
            _dataFetcher = dataFetcher;
            _aliasManager = aliasManager;
        }

        public IReadOnlyCollection<string> SkinlineNames => EnsureLoaded().DisplayNames.Values;

        public bool IsKnownSkinline(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            return _catalog.Champions.ContainsKey(NormalizeName(name));
        }

        public string GetDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            EnsureLoaded();
            string key = NormalizeName(name);
            return _catalog.DisplayNames.TryGetValue(key, out string displayName) ? displayName : name;
        }

        public IReadOnlyList<string> GetChampions(string name)
        {
            return GetChampionsWithSkin(name).Select(c => c.Name).ToList();
        }

        public IReadOnlyList<SkinlineChampion> GetChampionsWithSkin(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<SkinlineChampion>();
            EnsureLoaded();
            string key = NormalizeName(name);
            return _catalog.Champions.TryGetValue(key, out var list) ? list : new List<SkinlineChampion>();
        }

        private SkinlineCatalog EnsureLoaded()
        {
            if (_loaded) return _catalog;

            lock (_loadLock)
            {
                if (_loaded) return _catalog;

                _catalog = LoadFromDisk() ?? LoadFromNetwork().GetAwaiter().GetResult() ?? new SkinlineCatalog();
                _loaded = true;
                return _catalog;
            }
        }

        private async Task<SkinlineCatalog> LoadFromNetwork()
        {
            var catalog = new SkinlineCatalog();

            try
            {
                var skinLines = await _dataFetcher.GetSkinLinesAsync("EN");
                var allSkins = await _dataFetcher.GetSkinsDataAsync("EN");

                // Build a lookup: skinline id -> skinline name
                var lineIdToName = new Dictionary<int, string>();
                foreach (var line in skinLines)
                {
                    if (line.TryGetProperty("id", out var idProp) && line.TryGetProperty("name", out var nameProp))
                    {
                        int id = idProp.GetInt32();
                        string name = nameProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            lineIdToName[id] = name;
                            string key = NormalizeName(name);
                            if (!catalog.Champions.ContainsKey(key)) catalog.Champions[key] = new List<SkinlineChampion>();
                            if (!catalog.DisplayNames.ContainsKey(key)) catalog.DisplayNames[key] = name;
                        }
                    }
                }

                foreach (var skin in allSkins.Values)
                {
                    if (!skin.TryGetProperty("skinLines", out var slProp) || slProp.ValueKind != JsonValueKind.Array)
                        continue;

                    string splashPath = skin.TryGetProperty("splashPath", out var splashProp)
                        ? splashProp.GetString() ?? ""
                        : "";

                    var nameMatch = Regex.Match(splashPath, @"Characters/([^/]+)/");
                    if (!nameMatch.Success) continue;

                    string champInternalName = _aliasManager.GetInternalName(nameMatch.Groups[1].Value);
                    if (!_aliasManager.IsValidChampion(champInternalName)) continue;

                    var skinIdMatch = Regex.Match(splashPath, @"Skin(\d+)");
                    if (!skinIdMatch.Success) continue;
                    int skinId = int.Parse(skinIdMatch.Groups[1].Value);

                    foreach (var lineObj in slProp.EnumerateArray())
                    {
                        if (lineObj.TryGetProperty("id", out var lineIdProp) && lineIdProp.TryGetInt32(out int lineId))
                        {
                            if (lineIdToName.TryGetValue(lineId, out string lineName))
                            {
                                string key = NormalizeName(lineName);
                                if (catalog.Champions.TryGetValue(key, out var list))
                                {
                                    if (!list.Any(c => c.Name.Equals(champInternalName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        list.Add(new SkinlineChampion
                                        {
                                            Name = champInternalName,
                                            SkinId = skinId
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                SaveToDisk(catalog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading skinlines from network: {ex.Message}");
            }

            return catalog;
        }

        private SkinlineCatalog LoadFromDisk()
        {
            try
            {
                if (!File.Exists(AppConfig.SkinlineCachePath)) return null;

                string json = File.ReadAllText(AppConfig.SkinlineCachePath);
                var data = JsonSerializer.Deserialize<SkinlineCacheData>(json);
                if (data?.SkinlineMap != null)
                {
                    var catalog = new SkinlineCatalog();

                    foreach (var kvp in data.SkinlineMap)
                    {
                        string key = NormalizeName(kvp.Key);
                        catalog.Champions[key] = kvp.Value.Champions ?? new List<SkinlineChampion>();
                        catalog.DisplayNames[key] = kvp.Value.DisplayName ?? kvp.Key;
                    }

                    return catalog;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading skinlines from disk: {ex.Message}");
            }
            return null;
        }

        private void SaveToDisk(SkinlineCatalog catalog)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppConfig.SkinlineCachePath)!);

                var map = new Dictionary<string, SkinlineCacheEntry>();
                foreach (var kvp in catalog.DisplayNames)
                {
                    map[kvp.Key] = new SkinlineCacheEntry
                    {
                        DisplayName = kvp.Value,
                        Champions = catalog.Champions.TryGetValue(kvp.Key, out var list) ? list : new List<SkinlineChampion>()
                    };
                }

                var data = new SkinlineCacheData { SkinlineMap = map };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(AppConfig.SkinlineCachePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving skinlines to disk: {ex.Message}");
            }
        }

        public async Task<string> GetLocalizedDisplayNameAsync(string englishName, string language)
        {
            if (string.IsNullOrEmpty(englishName)) return englishName;

            try
            {
                var enLines = await _dataFetcher.GetSkinLinesAsync("EN");
                int targetId = -1;
                foreach (var line in enLines)
                {
                    if (line.TryGetProperty("name", out var nameProp) &&
                        englishName.Equals(nameProp.GetString(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (line.TryGetProperty("id", out var idProp))
                        {
                            targetId = idProp.GetInt32();
                            break;
                        }
                    }
                }

                if (targetId != -1)
                {
                    var locLines = await _dataFetcher.GetSkinLinesAsync(language);
                    foreach (var line in locLines)
                    {
                        if (line.TryGetProperty("id", out var idProp) && idProp.GetInt32() == targetId)
                        {
                            if (line.TryGetProperty("name", out var nameProp))
                            {
                                return nameProp.GetString() ?? englishName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting localized skinline name: {ex.Message}");
            }

            return GetDisplayName(englishName);
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return new string(name
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private class SkinlineCatalog
        {
            public Dictionary<string, List<SkinlineChampion>> Champions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> DisplayNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private class SkinlineCacheData
        {
            public Dictionary<string, SkinlineCacheEntry> SkinlineMap { get; set; } = new();
        }

        private class SkinlineCacheEntry
        {
            public string DisplayName { get; set; } = string.Empty;
            public List<SkinlineChampion> Champions { get; set; } = new();
        }
    }
}
