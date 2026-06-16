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
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class DataFetcher
    {
        private readonly HttpClient _httpClient;
        private Dictionary<string, JsonElement> _skinsCache;
        private List<JsonElement> _skinLinesCache;
        private Dictionary<int, ItemData> _itemsCache;

        private class ItemData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string NameSlug { get; set; }
        }

        public DataFetcher(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        private string _cachedVersion;

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
                Console.WriteLine($"Error getting LoL version: {ex.Message}");
                return _cachedVersion ?? "14.1.1";
            }
        }

        public async Task<Dictionary<string, JsonElement>> GetSkinsDataAsync()
        {
            if (_skinsCache != null) return _skinsCache;

            string cachePath = Path.Combine(AppConfig.CacheDir, "skins_data.json");
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading skins cache: {ex.Message}");
                }
            }

            return new Dictionary<string, JsonElement>();
        }

        public async Task<List<JsonElement>> GetSkinLinesAsync()
        {
            if (_skinLinesCache != null) return _skinLinesCache;

            string cachePath = Path.Combine(AppConfig.CacheDir, "skinlines_data.json");
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    return _skinLinesCache ?? new List<JsonElement>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading skinlines cache: {ex.Message}");
                }
            }

            return new List<JsonElement>();
        }

        private async Task<Dictionary<int, ItemData>> GetItemsDataAsync()
        {
            if (_itemsCache != null) return _itemsCache;

            string cachePath = Path.Combine(AppConfig.CacheDir, "items_data.json");
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _itemsCache = ParseItemsJson(cachedJson);
                    return _itemsCache ?? new Dictionary<int, ItemData>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading items cache: {ex.Message}");
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
                Console.WriteLine($"Error parsing items data: {ex.Message}");
            }
            return result;
        }

        public async Task<string> DownloadIconAsync(string url, string category, string customFileName = null)
        {
            try
            {
                string categoryDir = Path.Combine(AppConfig.IconCacheDir, category);
                Directory.CreateDirectory(categoryDir);
                
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
                await File.WriteAllBytesAsync(filePath, bytes);
                
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading icon from {url}: {ex.Message}");
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
            catch
            {
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
                Console.WriteLine($"Error resolving Fandom image URL for {filename}: {ex.Message}");
            }
            return null;
        }

        private Dictionary<string, string> _itemNameIdCache;
        private readonly object _itemCacheLock = new();
        private Dictionary<string, int> _communityItemNameToIdCache;

        private async Task<Dictionary<string, int>> GetCommunityItemNameToIdMapAsync()
        {
            if (_communityItemNameToIdCache != null) return _communityItemNameToIdCache;

            var itemsData = await GetItemsDataAsync();
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

            _communityItemNameToIdCache = map;
            return map;
        }

        public MonsterDatabase LoadMonsterDatabase()
        {
            try
            {
                if (File.Exists(AppConfig.MonstersPath))
                {
                    string json = File.ReadAllText(AppConfig.MonstersPath);
                    var db = JsonSerializer.Deserialize<MonsterDatabase>(json);
                    if (db != null) return db;
                }
            }
            catch
            {
                // Legacy flat list fallback
                try
                {
                    if (File.Exists(AppConfig.MonstersPath))
                    {
                        string json = File.ReadAllText(AppConfig.MonstersPath);
                        var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                        return new MonsterDatabase { Large = list };
                    }
                }
                catch { }
            }
            return new MonsterDatabase();
        }

        public async Task<(int Id, string Name)?> GetItemInfoAsync(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            var itemsData = await GetItemsDataAsync();
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
                Console.WriteLine($"Error resolving item via CommunityDragon: {ex.Message}");
            }

            // 2. Fallback to local DDragon items db
            if (_itemNameIdCache == null)
            {
                try
                {
                    if (File.Exists(AppConfig.ItemsPath))
                    {
                        string jsonStr = await File.ReadAllTextAsync(AppConfig.ItemsPath);
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
                    Console.WriteLine($"Error loading local items db: {ex.Message}");
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
