using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class DataFetcher
    {
        private readonly LogService _logger;
        private readonly string _storageRoot;
        private Dictionary<string, JsonElement> _skinsCache;
        private List<JsonElement> _skinLinesCache;
        private Dictionary<int, ItemData> _itemsCache;
        private string _loadedSkinsLocale;
        private string _loadedSkinlinesLocale;
        private string _loadedItemsLocale;
        private readonly object _cacheLock = new();
        private int _cacheVersion;

        // The bootstrapper writes new official JSON on game updates. In-flight reads
        // may finish after this call, so their cache writes must check _cacheVersion.
        public void InvalidateCaches()
        {
            lock (_cacheLock)
            {
                ++_cacheVersion;
                _skinsCache = null;
                _skinLinesCache = null;
                _itemsCache = null;
                _loadedSkinsLocale = null;
                _loadedSkinlinesLocale = null;
                _loadedItemsLocale = null;
                _itemNameIdCache = null;
                _communityItemNameToIdCache = null;
                _loadedCommunityItemsLocale = null;
            }
        }

        internal string StorageRoot => _storageRoot;
        private class ItemData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string NameSlug { get; set; }
        }

        public DataFetcher(LogService logger, string storageRoot = null)
        {
            _logger = logger;
            _storageRoot = storageRoot;
        }

        private string GetSkinsCachePath(string locale) =>
            AppConfig.GetSkinsCachePath(locale, _storageRoot);

        private string GetSkinLinesCachePath(string locale) =>
            AppConfig.GetSkinLinesCachePath(locale, _storageRoot);

        private string GetItemsCachePath(string locale) =>
            AppConfig.GetItemsCachePath(locale, _storageRoot);

        private string ItemsFilePath => AppConfig.GetItemsFilePath(_storageRoot);
        private string MonstersFilePath => AppConfig.GetMonstersFilePath(_storageRoot);

        // A Mapping edit must be observed by the next scan, without
        // rebuilding the EventNameParser or restarting VideoGenerator.
        // Checking file metadata is cheap; the JSON is parsed only when it
        // changes. Use both length and timestamp for ordinary saves.
        internal string MonsterCatalogRevision
        {
            get
            {
                try
                {
                    var info = new FileInfo(MonstersFilePath);
                    return info.Exists
                        ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}"
                        : "<missing>";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return "<unavailable>";
                }
            }
        }

        public async Task<Dictionary<string, JsonElement>> GetSkinsDataAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            while (true)
            {
                int version;
                lock (_cacheLock)
                {
                    if (_skinsCache != null && _loadedSkinsLocale == locale) return _skinsCache;
                    version = _cacheVersion;
                }

                Dictionary<string, JsonElement> rawSkins = null;
                string cachePath = GetSkinsCachePath(locale);
                if (File.Exists(cachePath))
                {
                    try
                    {
                        string cachedJson = await File.ReadAllTextAsync(cachePath);
                        rawSkins = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                        if (rawSkins != null)
                        {
                            var extraSkins = new Dictionary<string, JsonElement>();
                            foreach (var kvp in rawSkins)
                            {
                                var skinElement = kvp.Value;
                                if (skinElement.TryGetProperty("questSkinInfo", out var questInfoProp) &&
                                    questInfoProp.ValueKind == JsonValueKind.Object &&
                                    questInfoProp.TryGetProperty("tiers", out var tiersProp) &&
                                    tiersProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var tier in tiersProp.EnumerateArray())
                                    {
                                        if (tier.TryGetProperty("id", out var idProp))
                                        {
                                            string tierIdStr = idProp.ToString();
                                            if (!rawSkins.ContainsKey(tierIdStr) && !extraSkins.ContainsKey(tierIdStr))
                                            {
                                                extraSkins[tierIdStr] = tier;
                                            }
                                        }
                                    }
                                }
                            }

                            foreach (var kvp in extraSkins)
                                rawSkins[kvp.Key] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn("Failed to read the local skins cache. Skin data may be unavailable until the next synchronization.");
                        _logger.LogDebug($"Skins cache read details: {ex.Message}");
                    }
                }

                lock (_cacheLock)
                {
                    if (version != _cacheVersion) continue;
                    if (_skinsCache != null && _loadedSkinsLocale == locale) return _skinsCache;
                    if (rawSkins != null)
                    {
                        _skinsCache = rawSkins;
                        _loadedSkinsLocale = locale;
                    }
                    return rawSkins ?? new Dictionary<string, JsonElement>();
                }
            }
        }

        public async Task<List<JsonElement>> GetSkinLinesAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            while (true)
            {
                int version;
                lock (_cacheLock)
                {
                    if (_skinLinesCache != null && _loadedSkinlinesLocale == locale) return _skinLinesCache;
                    version = _cacheVersion;
                }

                List<JsonElement> parsed = null;
                string cachePath = GetSkinLinesCachePath(locale);
                if (File.Exists(cachePath))
                {
                    try
                    {
                        string cachedJson = await File.ReadAllTextAsync(cachePath);
                        parsed = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn("Failed to read the local skinlines cache. Skinline data may be unavailable until the next synchronization.");
                        _logger.LogDebug($"Skinlines cache read details: {ex.Message}");
                    }
                }

                lock (_cacheLock)
                {
                    if (version != _cacheVersion) continue;
                    if (_skinLinesCache != null && _loadedSkinlinesLocale == locale) return _skinLinesCache;
                    if (parsed != null)
                    {
                        _skinLinesCache = parsed;
                        _loadedSkinlinesLocale = locale;
                    }
                    return parsed ?? new List<JsonElement>();
                }
            }
        }

        private async Task<Dictionary<int, ItemData>> GetItemsDataAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            while (true)
            {
                int version;
                lock (_cacheLock)
                {
                    if (_itemsCache != null && _loadedItemsLocale == locale) return _itemsCache;
                    version = _cacheVersion;
                }

                // The dictionary language is a display preference, not a
                // requirement for the local game catalog to exist in that locale.
                // Always retain the default (English) IDs as a fallback. A
                // localized catalog may be absent or contain only some items.
                var parsed = new Dictionary<int, ItemData>();
                bool foundCatalog = false;
                string[] cacheLocales = locale == "default"
                    ? new[] { "default" }
                    : new[] { "default", locale };
                foreach (string cacheLocale in cacheLocales)
                {
                    string cachePath = GetItemsCachePath(cacheLocale);
                    if (!File.Exists(cachePath))
                        continue;
                    foundCatalog = true;

                    try
                    {
                        string cachedJson = await File.ReadAllTextAsync(cachePath);
                        foreach (var entry in ParseItemsJson(cachedJson))
                        {
                            if (parsed.TryGetValue(entry.Key, out var previous))
                            {
                                // Sparse locale records must not erase known
                                // English names or slugs for the same item ID.
                                if (!string.IsNullOrWhiteSpace(entry.Value.Name))
                                    previous.Name = entry.Value.Name;
                                if (!string.IsNullOrWhiteSpace(entry.Value.NameSlug))
                                    previous.NameSlug = entry.Value.NameSlug;
                            }
                            else
                            {
                                parsed[entry.Key] = entry.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn($"Failed to read the local CommunityDragon items cache '{Path.GetFileName(cachePath)}'. Item resolution may be limited.");
                        _logger.LogDebug($"Items cache read details: {ex.Message}");
                    }
                }

                lock (_cacheLock)
                {
                    if (version != _cacheVersion) continue;
                    if (_itemsCache != null && _loadedItemsLocale == locale) return _itemsCache;
                    // Keep missing-cache lookups retryable: bootstrap may write
                    // a new catalog later in the same process.
                    if (foundCatalog)
                    {
                        _itemsCache = parsed;
                        _loadedItemsLocale = locale;
                    }
                    return parsed;
                }
            }
        }

        private Dictionary<int, ItemData> ParseItemsJson(string json)
        {
            var result = new Dictionary<int, ItemData>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        // A single malformed entry must not discard the other
                        // valid records in a locally cached Riot items array.
                        if (item.ValueKind != JsonValueKind.Object ||
                            !item.TryGetProperty("id", out var idProp) ||
                            idProp.ValueKind != JsonValueKind.Number ||
                            !idProp.TryGetInt32(out int id))
                            continue;

                        var data = new ItemData { Id = id };
                        if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                            data.Name = nameProp.GetString();
                        if (item.TryGetProperty("nameSlug", out var slugProp) && slugProp.ValueKind == JsonValueKind.String)
                            data.NameSlug = slugProp.GetString();
                        result[data.Id] = data;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to parse CommunityDragon item data.", ex);
            }
            return result;
        }

        // Shared by the local game icon resolver for existing image-cache entries.
        internal static Task<bool> IsUsableCachedIconAsync(string path) =>
            Task.Run(() => IsValidCachedImage(path));

        private static bool IsValidCachedImage(string path)
        {
            try
            {
                return File.Exists(path) && IsDecodableImage(File.ReadAllBytes(path));
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static bool IsDecodableImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            try
            {
                using var image = Image.Load(bytes);
                return image.Width > 0 && image.Height > 0;
            }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException or ArgumentException)
            {
                return false;
            }
        }

        private Dictionary<string, string> _itemNameIdCache;
        private Dictionary<string, int> _communityItemNameToIdCache;
        private string _loadedCommunityItemsLocale;

        private async Task<Dictionary<string, int>> GetCommunityItemNameToIdMapAsync(string language)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            while (true)
            {
                int version;
                lock (_cacheLock)
                {
                    if (_communityItemNameToIdCache != null && _loadedCommunityItemsLocale == locale)
                        return _communityItemNameToIdCache;
                    version = _cacheVersion;
                }

                // Include English names and slugs even when the selected
                // language has localized item display names.
                var defaultItems = await GetItemsDataAsync("EN");
                var localizedItems = locale == "default"
                    ? defaultItems
                    : await GetItemsDataAsync(language);
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                void AddNames(IEnumerable<ItemData> items)
                {
                    foreach (var item in items)
                    {
                        foreach (string name in new[] { item.Name, item.NameSlug })
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            map[name] = item.Id;
                            string normalized = Regex.Replace(name, @"[^A-Za-z0-9]", "");
                            if (!map.ContainsKey(normalized))
                                map[normalized] = item.Id;
                        }
                    }
                }

                AddNames(defaultItems.Values);
                if (locale != "default")
                    AddNames(localizedItems.Values);
                map["Ward"] = 3340;
                lock (_cacheLock)
                {
                    if (version != _cacheVersion) continue;
                    if (_communityItemNameToIdCache != null && _loadedCommunityItemsLocale == locale)
                        return _communityItemNameToIdCache;
                    _communityItemNameToIdCache = map;
                    _loadedCommunityItemsLocale = locale;
                    return map;
                }
            }
        }

        public MonsterDatabase LoadMonsterDatabase()
        {
            try
            {
                if (File.Exists(MonstersFilePath))
                {
                    string json = File.ReadAllText(MonstersFilePath);
                    var db = JsonSerializer.Deserialize<MonsterDatabase>(json);
                    if (db != null) return db;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load the monsters database. Trying the legacy format.", ex);
                // Legacy flat list fallback
                try
                {
                    if (File.Exists(MonstersFilePath))
                    {
                        string json = File.ReadAllText(MonstersFilePath);
                        var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                        return new MonsterDatabase { Large = list };
                    }
                }
                catch (Exception legacyEx)
                {
                    _logger.LogError("Failed to load the legacy monsters database format.", legacyEx);
                }
            }
            return new MonsterDatabase();
        }

        public async Task<(int Id, string Name)?> GetItemInfoAsync(string itemName, string language = null)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            var itemsData = await GetItemsDataAsync(language);
            if (itemsData == null || itemsData.Count == 0) return null;

            // Direct numeric ID
            if (int.TryParse(itemName, out int numericId) && itemsData.TryGetValue(numericId, out var numericItem))
            {
                return (numericId, numericItem.Name ?? numericItem.NameSlug ?? itemName);
            }

            var nameToIdMap = await GetCommunityItemNameToIdMapAsync(language);
            if (nameToIdMap == null || nameToIdMap.Count == 0) return null;

            // Exact or normalized match
            if (nameToIdMap.TryGetValue(itemName, out int id))
            {
                if (itemsData.TryGetValue(id, out var item))
                    return (id, item.Name ?? item.NameSlug ?? itemName);
            }

            string cleanSearch = Regex.Replace(itemName, @"[^A-Za-z0-9]", "");
            if (nameToIdMap.TryGetValue(cleanSearch, out id))
            {
                if (itemsData.TryGetValue(id, out var item))
                    return (id, item.Name ?? item.NameSlug ?? itemName);
            }

            // Partial match
            var bestMatch = nameToIdMap.Keys.FirstOrDefault(k => k.Contains(itemName, StringComparison.OrdinalIgnoreCase));
            if (bestMatch != null)
            {
                id = nameToIdMap[bestMatch];
                if (itemsData.TryGetValue(id, out var item))
                    return (id, item.Name ?? item.NameSlug ?? itemName);
            }

            var bestMatchClean = nameToIdMap.Keys.FirstOrDefault(k => k.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase));
            if (bestMatchClean != null)
            {
                id = nameToIdMap[bestMatchClean];
                if (itemsData.TryGetValue(id, out var item))
                    return (id, item.Name ?? item.NameSlug ?? itemName);
            }

            return null;
        }

        public async Task<string> ResolveItemNameToIdAsync(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            if (int.TryParse(itemName, out _)) return itemName; // already an ID

            // 1. Read the installed game's items catalog from its local JSON cache.
            try
            {
                var info = await GetItemInfoAsync(itemName);
                if (info.HasValue)
                    return info.Value.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to resolve the item through the local game catalog. The local item database will be used as fallback.");
                _logger.LogDebug($"Local item catalog details: {ex.Message}");
            }

            // 2. Fallback to the preserved local item name-to-ID database.
            Dictionary<string, string> itemNameIdCache;
            while (true)
            {
                int version;
                lock (_cacheLock)
                {
                    itemNameIdCache = _itemNameIdCache;
                    version = _cacheVersion;
                }

                if (itemNameIdCache != null || !File.Exists(ItemsFilePath)) break;

                try
                {
                    string jsonStr = await File.ReadAllTextAsync(ItemsFilePath);
                    var baseMap = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonStr) ?? new Dictionary<string, string>();

                    var tempMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in baseMap)
                    {
                        tempMap[kvp.Key] = kvp.Value;
                        string normalized = Regex.Replace(kvp.Key, @"[^A-Za-z0-9]", "");
                        if (!tempMap.ContainsKey(normalized))
                        {
                            tempMap[normalized] = kvp.Value;
                        }
                    }

                    lock (_cacheLock)
                    {
                        if (version != _cacheVersion) continue;
                        itemNameIdCache = _itemNameIdCache ??= tempMap;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load the local item name-to-ID database.", ex);
                    return null;
                }
            }

            if (itemNameIdCache != null)
            {
                // 1. Direct exact or normalized match
                if (itemNameIdCache.TryGetValue(itemName, out string id))
                {
                    return id;
                }

                string cleanSearch = Regex.Replace(itemName, @"[^A-Za-z0-9]", "");
                if (itemNameIdCache.TryGetValue(cleanSearch, out id))
                {
                    return id;
                }

                // 2. Partial match
                var bestMatch = itemNameIdCache.Keys.FirstOrDefault(k => k.Contains(itemName, StringComparison.OrdinalIgnoreCase));
                if (bestMatch != null)
                {
                    return itemNameIdCache[bestMatch];
                }

                var bestMatchClean = itemNameIdCache.Keys.FirstOrDefault(k => k.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase));
                if (bestMatchClean != null)
                {
                    return itemNameIdCache[bestMatchClean];
                }
            }

            return null;
        }
    }
}
