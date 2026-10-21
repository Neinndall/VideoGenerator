using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Centralized authority for League of Legends skinlines.
    /// Reads locally cached Riot skinline metadata and provides fast lookups.
    /// </summary>
    public class SkinlineManager
    {
        private readonly DataFetcher _dataFetcher;
        private readonly AliasManager _aliasManager;
        private readonly LogService _logger;

        private SkinlineCatalog _catalog;
        private bool _loaded;
        private readonly object _loadLock = new();
        private readonly string _catalogPath;

        public SkinlineManager(DataFetcher dataFetcher, AliasManager aliasManager, LogService logger)
        {
            _dataFetcher = dataFetcher;
            _aliasManager = aliasManager;
            _logger = logger;
            _catalogPath = Path.Combine(AppConfig.GetConfigDirectory(dataFetcher.StorageRoot), "skinlines.json");
        }

        public IReadOnlyCollection<string> SkinlineNames => EnsureLoaded().DisplayNames.Values;

        /// <summary>
        /// Re-evaluates the official local JSON caches on the next lookup.
        /// Call after the game-data bootstrapper refreshes its on-disk catalogs.
        /// </summary>
        public void InvalidateCatalog()
        {
            lock (_loadLock)
            {
                _dataFetcher.InvalidateCaches();
                _catalog = null;
                _loaded = false;
            }
        }

        public bool IsKnownSkinline(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var catalog = EnsureLoaded();
            return catalog.Champions.ContainsKey(NormalizeName(name)) ||
                catalog.DisplayNames.Values.Any(official =>
                    LeagueSkinlineIdentity.Matches(name, official));
        }

        public string GetDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string key = NormalizeName(name);
            var catalog = EnsureLoaded();
            if (catalog.DisplayNames.TryGetValue(key, out string displayName))
                return displayName;
            string exactAlias = catalog.DisplayNames.Values.FirstOrDefault(official =>
                LeagueSkinlineIdentity.Matches(name, official) &&
                LeagueSkinlineIdentity.GetFamilyName(official) == null);
            if (exactAlias != null) return exactAlias;
            var family = catalog.DisplayNames.Values
                .Select(LeagueSkinlineIdentity.GetFamilyName)
                .FirstOrDefault(candidate => candidate != null &&
                    LeagueSkinlineIdentity.Normalize(candidate) == key);
            return family ?? name;
        }

        public IReadOnlyList<string> GetChampions(string name)
        {
            return GetChampionsWithSkin(name).Select(c => c.Name).ToList();
        }

        public IReadOnlyList<SkinlineChampion> GetChampionsWithSkin(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<SkinlineChampion>();
            string key = NormalizeName(name);
            var catalog = EnsureLoaded();
            if (catalog.Champions.TryGetValue(key, out var list)) return list;

            // Riot may split a thematic family into colon-qualified lines.
            // Keep both Arcade: Heroes and Arcade: Battle Bosses as possible
            // visual members when the event simply says "Arcade".
            return catalog.DisplayNames
                .Where(pair => LeagueSkinlineIdentity.Matches(name, pair.Value))
                .SelectMany(pair => catalog.Champions.TryGetValue(pair.Key, out var members)
                    ? members : Enumerable.Empty<SkinlineChampion>())
                .GroupBy(member => (member.Name.ToLowerInvariant(), member.SkinId))
                .Select(group => group.First())
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(member => member.SkinId)
                .ToList();
        }

        /// <summary>
        /// VO labels such as "WorldsWinners" and "T1WorldsWinners" are not
        /// official skinline names. Resolve the speaking skin to its actual
        /// "World Champions: YEAR" line using Riot's cached membership, never
        /// by guessing a championship year or borrowing a different team.
        /// </summary>
        public async Task<string> FindWorldChampionsLineForSkinAsync(
            string champion, int skinId, bool requireT1)
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0) return null;
            string internalName = _aliasManager.GetInternalName(champion);
            var catalog = EnsureLoaded();
            string officialLine = catalog.DisplayNames
                .Where(pair => string.Equals(
                    LeagueSkinlineIdentity.GetFamilyName(pair.Value),
                    "World Champions", StringComparison.OrdinalIgnoreCase))
                .Where(pair => catalog.Champions.TryGetValue(pair.Key, out var members) &&
                    members.Any(member => member.SkinId == skinId &&
                        string.Equals(_aliasManager.GetInternalName(member.Name),
                            internalName, StringComparison.OrdinalIgnoreCase)))
                .Select(pair => pair.Value)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (officialLine == null || !requireT1) return officialLine;

            // A "World Champions" year may belong to DRX, EDG, etc. The T1
            // qualifier must also be confirmed by the *specific* Riot skin
            // name (e.g. "MVP T1 Miss Fortune"), not merely by its skinline.
            var skins = await _dataFetcher.GetSkinsDataAsync("EN");
            foreach (var skin in skins.Values)
            {
                if (skin.ValueKind != JsonValueKind.Object ||
                    !skin.TryGetProperty("splashPath", out var splash) ||
                    splash.ValueKind != JsonValueKind.String)
                    continue;
                Match match = Regex.Match(splash.GetString() ?? "",
                    @"[/\\]Characters[/\\]([^/\\]+)[/\\]Skins[/\\]Skin(\d+)[/\\]",
                    RegexOptions.IgnoreCase);
                if (!match.Success || !int.TryParse(match.Groups[2].Value, out int id) ||
                    id != skinId ||
                    !string.Equals(_aliasManager.GetInternalName(match.Groups[1].Value),
                        internalName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (skin.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    Regex.IsMatch(name.GetString() ?? "", @"\bT1\b",
                        RegexOptions.IgnoreCase))
                    return officialLine;
            }
            return null;
        }

        private SkinlineCatalog EnsureLoaded()
        {
            lock (_loadLock)
            {
                if (_loaded) return _catalog;

                // A generated skinlines.json may have a newer timestamp but
                // contain zero members (e.g. it was built before champions.json
                // was populated). The official WAD-derived skinlines and skins
                // caches are authoritative whenever BOTH are usable, regardless
                // of last-write order. Never overwrite the user's generated file.
                _catalog = OfficialCachesPresent()
                    ? Task.Run(LoadFromLocalCatalogAsync).GetAwaiter().GetResult() ?? LoadFromDisk()
                    : LoadFromDisk();
                _catalog ??= new SkinlineCatalog();
                _loaded = true;
                return _catalog;
            }
        }

        private bool OfficialCachesPresent()
        {
            string skinsPath = AppConfig.GetSkinsCachePath("default", _dataFetcher.StorageRoot);
            string skinlinesPath = AppConfig.GetSkinLinesCachePath("default", _dataFetcher.StorageRoot);
            return File.Exists(skinsPath) && File.Exists(skinlinesPath);
        }

        private async Task<SkinlineCatalog> LoadFromLocalCatalogAsync()
        {
            var catalog = new SkinlineCatalog();

            try
            {
                var skinLines = await _dataFetcher.GetSkinLinesAsync("EN");
                var allSkins = await _dataFetcher.GetSkinsDataAsync("EN");
                if (skinLines.Count == 0 || allSkins.Count == 0)
                    return null;

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

                    var nameMatch = Regex.Match(splashPath, @"Characters[/\\]([^/\\]+)[/\\]", RegexOptions.IgnoreCase);
                    if (!nameMatch.Success) continue;

                    string champInternalName = _aliasManager.GetInternalName(nameMatch.Groups[1].Value);
                    // Riot's own skins.json is authoritative for identities.
                    // champions.json can be missing/empty on a fresh install,
                    // so filtering these members through the local champion
                    // alias list silently empties entire skinlines.
                    if (string.IsNullOrWhiteSpace(champInternalName)) continue;

                    var skinIdMatch = Regex.Match(splashPath, @"Skin(\d+)", RegexOptions.IgnoreCase);
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
                                    if (!list.Any(c => c.Name.Equals(champInternalName, StringComparison.OrdinalIgnoreCase) &&
                                                       c.SkinId == skinId))
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

                // This file can contain user edits. Only create the generated
                // fallback when no configuration exists yet.
                if (!File.Exists(_catalogPath))
                    SaveToDisk(catalog);
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to load the locally cached skinline catalog. Falling back to local defaults.");
                _logger.LogDebug($"Local skinline catalog details: {ex.Message}");
                return null;
            }

            return catalog;
        }

        private SkinlineCatalog LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_catalogPath)) return null;

                string json = File.ReadAllText(_catalogPath);
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
                _logger.LogError("Failed to load the local skinline cache.", ex);
            }
            return null;
        }

        private void SaveToDisk(SkinlineCatalog catalog)
        {
            try
            {
                DirectoriesCreator.CreateParentDirectory(_catalogPath);

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
                File.WriteAllText(_catalogPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save the local skinline cache.", ex);
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
                _logger.LogWarn("Failed to resolve the localized skinline name. The default name will be used.");
                _logger.LogDebug($"Localized skinline lookup details: {ex.Message}");
            }

            return GetDisplayName(englishName);
        }

        private static string NormalizeName(string name)
        {
            return LeagueSkinlineIdentity.Normalize(name);
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
