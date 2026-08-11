using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class DataFetcher
    {
        private readonly HttpClient _httpClient;
        private readonly LogService _logger;
        private readonly string _cacheDirectory;
        private readonly string _iconCacheDirectory;
        private readonly string _itemsFilePath;
        private readonly string _monstersFilePath;
        private Dictionary<string, JsonElement> _skinsCache;
        private List<JsonElement> _skinLinesCache;
        private Dictionary<int, ItemData> _itemsCache;
        private string _loadedSkinsLocale;
        private string _loadedSkinlinesLocale;
        private string _loadedItemsLocale;

        private class ItemData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string NameSlug { get; set; }
        }

        public DataFetcher(
            HttpClient httpClient,
            LogService logger,
            string storageRoot = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            bool useIsolatedStorage = !string.IsNullOrWhiteSpace(storageRoot);
            string configDirectory = useIsolatedStorage
                ? Path.Combine(storageRoot, "Config")
                : AppConfig.ConfigDir;
            _cacheDirectory = useIsolatedStorage
                ? Path.Combine(storageRoot, "Cache")
                : AppConfig.CacheDir;
            _iconCacheDirectory = Path.Combine(_cacheDirectory, "IconCache");
            _itemsFilePath = useIsolatedStorage
                ? Path.Combine(configDirectory, "items.json")
                : AppConfig.ItemsPath;
            _monstersFilePath = useIsolatedStorage
                ? Path.Combine(configDirectory, "monsters.json")
                : AppConfig.MonstersPath;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        private string _cachedVersion;

        private string GetSkinsCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"skins_data_{locale}.json");

        private string GetSkinLinesCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"skinlines_data_{locale}.json");

        private string GetItemsCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"items_data_{locale}.json");

        public async Task<string> GetLatestLolVersionAsync()
        {
            if (!string.IsNullOrEmpty(_cachedVersion)) return _cachedVersion;
            try
            {
                var response = await _httpClient.GetStringAsync(AppConfig.VersionsUrl);
                var versions = JsonSerializer.Deserialize<List<string>>(response);
                _cachedVersion = versions?.FirstOrDefault() ?? "14.1.1";
                return _cachedVersion;
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to retrieve the latest League of Legends version. The cached or fallback version will be used.");
                _logger.LogDebug($"LoL version request details: {ex.Message}");
                return _cachedVersion ?? "14.1.1";
            }
        }

        public async Task<Dictionary<string, JsonElement>> GetSkinsDataAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            if (_skinsCache != null && _loadedSkinsLocale == locale) return _skinsCache;

            string cachePath = GetSkinsCachePath(locale);
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    var rawSkins = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                    if (rawSkins != null)
                    {
                        var extraSkins = new Dictionary<string, JsonElement>();
                        foreach (var kvp in rawSkins)
                        {
                            var skinElement = kvp.Value;
                            if (skinElement.TryGetProperty("questSkinInfo", out var questInfoProp) && 
                                questInfoProp.ValueKind == JsonValueKind.Object)
                            {
                                if (questInfoProp.TryGetProperty("tiers", out var tiersProp) && 
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
                        }

                        foreach (var kvp in extraSkins)
                        {
                            rawSkins[kvp.Key] = kvp.Value;
                        }
                        _skinsCache = rawSkins;
                    }
                    _loadedSkinsLocale = locale;
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarn("Failed to read the local skins cache. Skin data may be unavailable until the next synchronization.");
                    _logger.LogDebug($"Skins cache read details: {ex.Message}");
                }
            }

            return new Dictionary<string, JsonElement>();
        }

        public async Task<List<JsonElement>> GetSkinLinesAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            if (_skinLinesCache != null && _loadedSkinlinesLocale == locale) return _skinLinesCache;

            string cachePath = GetSkinLinesCachePath(locale);
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    _loadedSkinlinesLocale = locale;
                    return _skinLinesCache ?? new List<JsonElement>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarn("Failed to read the local skinlines cache. Skinline data may be unavailable until the next synchronization.");
                    _logger.LogDebug($"Skinlines cache read details: {ex.Message}");
                }
            }

            return new List<JsonElement>();
        }

        private async Task<Dictionary<int, ItemData>> GetItemsDataAsync(string language = null)
        {
            string locale = AppConfig.GetCdragonLocale(language);
            if (_itemsCache != null && _loadedItemsLocale == locale) return _itemsCache;

            string cachePath = GetItemsCachePath(locale);
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _itemsCache = ParseItemsJson(cachedJson);
                    _loadedItemsLocale = locale;
                    return _itemsCache ?? new Dictionary<int, ItemData>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarn("Failed to read the local CommunityDragon items cache. Item resolution may be limited.");
                    _logger.LogDebug($"Items cache read details: {ex.Message}");
                }
            }

            return new Dictionary<int, ItemData>();
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
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            var data = new ItemData { Id = idProp.GetInt32() };
                            if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                                data.Name = nameProp.GetString();
                            if (item.TryGetProperty("nameSlug", out var slugProp) && slugProp.ValueKind == JsonValueKind.String)
                                data.NameSlug = slugProp.GetString();
                            result[data.Id] = data;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to parse CommunityDragon item data.", ex);
            }
            return result;
        }

        public async Task<string> DownloadIconAsync(string url, string category, string customFileName = null)
        {
            try
            {
                string categoryDir = Path.Combine(_iconCacheDirectory, category);
                DirectoriesCreator.CreateDirectory(categoryDir);
                
                string fileName = customFileName ?? Path.GetFileName(new Uri(url).LocalPath);
                // Strip query parameters if customFileName wasn't provided
                if (customFileName == null)
                {
                    int queryIdx = fileName.IndexOf('?');
                    if (queryIdx >= 0) fileName = fileName.Substring(0, queryIdx);
                }
                
                string filePath = Path.Combine(categoryDir, fileName);

                if (File.Exists(filePath)) return filePath;

                var bytes = await _httpClient.GetByteArrayAsync(url);
                string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.download";
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, bytes);
                    File.Move(temporaryPath, filePath, true);
                }
                finally
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                }
                
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to download icon from {url}. Details: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> GetAllItemIconFilenamesAsync()
        {
            try
            {
                string url = "https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/assets/items/icons2d/";
                var html = await _httpClient.GetStringAsync(url);
                var matches = Regex.Matches(html, @"<a href=""([^""]+\.png)""");
                return matches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to retrieve the item icon catalog.");
                _logger.LogDebug($"Item icon catalog details: {ex.Message}");
                return new List<string>();
            }
        }

        public string GetFandomImageUrl(string filename)
        {
            // Legacy direct redirect path; kept for compatibility.
            // Prefer ResolveFandomImageUrlAsync which uses the MediaWiki API to bypass Cloudflare.
            string encodedName = Uri.EscapeDataString(filename.Replace(" ", "_"));
            return $"https://leagueoflegends.fandom.com/wiki/Special:FilePath/{encodedName}";
        }

        public async Task<string> ResolveFandomImageUrlAsync(string filename)
        {
            try
            {
                string encodedTitle = Uri.EscapeDataString("File:" + filename.Replace(" ", "_"));
                string apiUrl = $"https://leagueoflegends.fandom.com/api.php?action=query&titles={encodedTitle}&prop=imageinfo&iiprop=url&format=json";
                string json = await _httpClient.GetStringAsync(apiUrl);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("query", out var query) &&
                    query.TryGetProperty("pages", out var pages))
                {
                    foreach (var page in pages.EnumerateObject())
                    {
                        if (page.Value.TryGetProperty("missing", out _)) continue;
                        if (page.Value.TryGetProperty("imageinfo", out var imageInfo) &&
                            imageInfo.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var info in imageInfo.EnumerateArray())
                            {
                                if (info.TryGetProperty("url", out var urlProp))
                                {
                                    string resolved = urlProp.GetString();
                                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarn($"Failed to resolve the Fandom image URL for '{filename}'.");
                _logger.LogDebug($"Fandom image resolution details: {ex.Message}");
            }
            return null;
        }

        private Dictionary<string, string> _itemNameIdCache;
        private readonly object _itemCacheLock = new();
        private Dictionary<string, int> _communityItemNameToIdCache;

        private async Task<Dictionary<string, int>> GetCommunityItemNameToIdMapAsync()
        {
            if (_communityItemNameToIdCache != null) return _communityItemNameToIdCache;

            var itemsData = await GetItemsDataAsync("EN");
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in itemsData.Values)
            {
                var namesToTry = new List<string>();
                if (!string.IsNullOrEmpty(item.Name))
                    namesToTry.Add(item.Name);
                if (!string.IsNullOrEmpty(item.NameSlug))
                    namesToTry.Add(item.NameSlug);

                foreach (var name in namesToTry.Where(n => !string.IsNullOrEmpty(n)))
                {
                    map[name] = item.Id;
                    string normalized = Regex.Replace(name, @"[^A-Za-z0-9]", "");
                    if (!map.ContainsKey(normalized))
                        map[normalized] = item.Id;
                }
            }

            map["Ward"] = 3340;
            _communityItemNameToIdCache = map;
            return map;
        }

        public MonsterDatabase LoadMonsterDatabase()
        {
            try
            {
                if (File.Exists(_monstersFilePath))
                {
                    string json = File.ReadAllText(_monstersFilePath);
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
                    if (File.Exists(_monstersFilePath))
                    {
                        string json = File.ReadAllText(_monstersFilePath);
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

            var nameToIdMap = await GetCommunityItemNameToIdMapAsync();
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

            // 1. Try CommunityDragon items data first (cached locally in Cache/items_data.json)
            try
            {
                var info = await GetItemInfoAsync(itemName);
                if (info.HasValue)
                    return info.Value.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to resolve the item through CommunityDragon. The local item database will be used as fallback.");
                _logger.LogDebug($"CommunityDragon item resolution details: {ex.Message}");
            }

            // 2. Fallback to local DDragon items db
            if (_itemNameIdCache == null)
            {
                try
                {
                    if (File.Exists(_itemsFilePath))
                    {
                        string jsonStr = await File.ReadAllTextAsync(_itemsFilePath);
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

                        lock (_itemCacheLock)
                        {
                            _itemNameIdCache = tempMap;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load the local Data Dragon item database.", ex);
                    return null;
                }
            }

            if (_itemNameIdCache != null)
            {
                // 1. Direct exact or normalized match
                if (_itemNameIdCache.TryGetValue(itemName, out string id))
                {
                    return id;
                }

                string cleanSearch = Regex.Replace(itemName, @"[^A-Za-z0-9]", "");
                if (_itemNameIdCache.TryGetValue(cleanSearch, out id))
                {
                    return id;
                }

                // 2. Partial match
                var bestMatch = _itemNameIdCache.Keys.FirstOrDefault(k => k.Contains(itemName, StringComparison.OrdinalIgnoreCase));
                if (bestMatch != null)
                {
                    return _itemNameIdCache[bestMatch];
                }

                var bestMatchClean = _itemNameIdCache.Keys.FirstOrDefault(k => k.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase));
                if (bestMatchClean != null)
                {
                    return _itemNameIdCache[bestMatchClean];
                }
            }

            return null;
        }
    }
}
