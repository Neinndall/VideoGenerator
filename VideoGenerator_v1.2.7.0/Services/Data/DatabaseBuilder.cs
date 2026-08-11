using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class DatabaseBuilder
    {
        private readonly HttpClient _httpClient;
        private readonly LogService _logger;
        private readonly string _championsPath;
        private readonly string _itemsPath;
        private readonly string _monstersPath;
        private readonly string _structuresPath;
        private readonly string _localVersionPath;
        private readonly string _cacheDirectory;
        private readonly SemaphoreSlim _initializationGate = new(1, 1);
        private readonly TaskCompletionSource<bool> _initializationReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ReadyTask => _initializationReady.Task;
        public bool IsReady => _initializationReady.Task.IsCompleted;

        public DatabaseBuilder(
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
            _championsPath = Path.Combine(configDirectory, "champions.json");
            _itemsPath = Path.Combine(configDirectory, "items.json");
            _monstersPath = Path.Combine(configDirectory, "monsters.json");
            _structuresPath = Path.Combine(configDirectory, "structures.json");
            _localVersionPath = Path.Combine(configDirectory, "version.json");
        }

        public async Task InitializeDatabasesAsync(string currentLolVersion)
        {
            await _initializationGate.WaitAsync();
            try
            {
                if (_initializationReady.Task.IsCompleted)
                    return;

                bool versionChanged = CheckIfVersionChanged(currentLolVersion);

                // 1. Sync Champions and Items from DDragon if version changed or files missing
                if (versionChanged || !File.Exists(_championsPath))
                {
                    await SyncChampionsAsync(currentLolVersion);
                }

                if (versionChanged || !File.Exists(_itemsPath))
                {
                    await SyncItemsAsync(currentLolVersion);
                }

                // 2. Sync CommunityDragon data (skins, skinlines, full items database)
                // The conditional request keeps valid local files while still checking
                // the server for updates on every application session.
                string defaultLocale = AppConfig.GetCdragonLocale();
                await Task.WhenAll(
                    SyncCommunityDragonJsonAsync(AppConfig.GetSkinsDataUrl(defaultLocale), GetSkinsCachePath(defaultLocale)),
                    SyncCommunityDragonJsonAsync(AppConfig.GetSkinLinesUrl(defaultLocale), GetSkinLinesCachePath(defaultLocale)),
                    SyncCommunityDragonJsonAsync(AppConfig.GetItemsDataUrl(defaultLocale), GetItemsCachePath(defaultLocale))
                );

                // 3. Fandom sync (Monsters/Structures) - These are lore-based, so we always try to merge new ones
                var epicMonsters = await SyncFandomCategoryAsync("Epic_monsters");
                var largeMonsters = await SyncFandomCategoryAsync("Large_monsters");
                await SaveMonstersDatabaseAsync(epicMonsters, largeMonsters);
                var structureNames = await SyncFandomCategoryAsync("Structures");
                await SaveStructuresDatabaseAsync(structureNames);

                // 4. Save the new version locally if sync was successful
                if (versionChanged)
                {
                    SaveLocalVersion(currentLolVersion);
                }
            }
            finally
            {
                // A failed network sync must not deadlock production. Consumers
                // can continue with any valid cache already present on disk.
                _initializationReady.TrySetResult(true);
                _initializationGate.Release();
            }
        }

        private string GetSkinsCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"skins_data_{locale}.json");

        private string GetSkinLinesCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"skinlines_data_{locale}.json");

        private string GetItemsCachePath(string locale) =>
            Path.Combine(_cacheDirectory, $"items_data_{locale}.json");

        private async Task SyncCommunityDragonJsonAsync(string url, string cachePath)
        {
            try
            {
                DirectoriesCreator.CreateParentDirectory(cachePath);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (File.Exists(cachePath))
                {
                    request.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(cachePath);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    string temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
                    try
                    {
                        await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8);
                        File.Move(temporaryPath, cachePath, true);
                    }
                    finally
                    {
                        try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to sync CommunityDragon data from {url}. Existing cache will be used when available.", ex);
            }
        }

        private bool CheckIfVersionChanged(string currentVersion)
        {
            try
            {
                if (!File.Exists(_localVersionPath)) return true;
                string localVersion = File.ReadAllText(_localVersionPath).Trim();
                return !localVersion.Equals(currentVersion, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarn("Failed to read the locally stored League of Legends version. A synchronization will be attempted.");
                _logger.LogDebug($"Version cache read details: {ex.Message}");
                return true;
            }
        }

        private void SaveLocalVersion(string version)
        {
            try
            {
                string temporaryPath = $"{_localVersionPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(temporaryPath, version, Encoding.UTF8);
                    File.Move(temporaryPath, _localVersionPath, true);
                }
                finally
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save the locally synchronized League of Legends version.", ex);
            }
        }

        private async Task SyncChampionsAsync(string version)
        {
            try
            {
                string url = $"https://ddragon.leagueoflegends.com/cdn/{version}/data/en_US/champion.json";
                string jsonStr = await _httpClient.GetStringAsync(url);
                
                using (JsonDocument doc = JsonDocument.Parse(jsonStr))
                {
                    var dataProp = doc.RootElement.GetProperty("data");
                    var championsList = new List<string>();
                    
                    foreach (var champ in dataProp.EnumerateObject())
                    {
                        championsList.Add(champ.Name); // Internal ID (e.g. MonkeyKing)
                        string name = champ.Value.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(name) && !championsList.Contains(name))
                        {
                            championsList.Add(name); // Display Name (e.g. Wukong)
                        }
                    }

                    SaveToJson(_championsPath, championsList.OrderBy(x => x).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to sync the champion database from Data Dragon. Existing data will be used when available.", ex);
            }
        }

        private async Task SyncItemsAsync(string version)
        {
            try
            {
                string url = $"https://ddragon.leagueoflegends.com/cdn/{version}/data/en_US/item.json";
                string jsonStr = await _httpClient.GetStringAsync(url);
                
                using (JsonDocument doc = JsonDocument.Parse(jsonStr))
                {
                    var dataProp = doc.RootElement.GetProperty("data");
                    var itemsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var itemProp in dataProp.EnumerateObject())
                    {
                        string id = itemProp.Name;
                        string name = itemProp.Value.GetProperty("name").GetString();
                        
                        if (!string.IsNullOrEmpty(name))
                        {
                            itemsDict[name] = id; // "Infinity Edge": "3031"
                        }
                    }

                    SaveToJson(_itemsPath, itemsDict);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to sync the item database from Data Dragon. Existing data will be used when available.", ex);
            }
        }

        private async Task<List<string>> SyncFandomCategoryAsync(string category, string savePath = null)
        {
            var result = new List<string>();
            try
            {
                string url = $"https://leagueoflegends.fandom.com/api.php?action=query&list=categorymembers&cmtitle=Category:{category}&cmnamespace=0&cmlimit=100&format=json";
                string jsonStr = await _httpClient.GetStringAsync(url);
                
                using (JsonDocument doc = JsonDocument.Parse(jsonStr))
                {
                    var members = doc.RootElement.GetProperty("query").GetProperty("categorymembers");
                    var newItems = new List<string>();
                    
                    foreach (var member in members.EnumerateArray())
                    {
                        string title = member.GetProperty("title").GetString();
                        if (!string.IsNullOrEmpty(title))
                        {
                            // Clean Fandom junk
                            string cleanTitle = title.Replace("/LoL", "").Replace(" camp", "");
                            newItems.Add(cleanTitle);
                        }
                    }

                    if (string.IsNullOrEmpty(savePath))
                    {
                        return newItems.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                    }

                    // Smart Merge
                    var existingItems = new List<string>();
                    if (File.Exists(savePath))
                    {
                        try
                        {
                            string existingJson = File.ReadAllText(savePath);
                            var loaded = JsonSerializer.Deserialize<List<string>>(existingJson);
                            if (loaded != null) existingItems = loaded;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarn($"Failed to read the existing Fandom database for '{category}'. Newly synchronized entries will be used.");
                            _logger.LogDebug($"Existing Fandom database details: {ex.Message}");
                        }
                    }

                    bool merged = false;
                    foreach (var item in newItems)
                    {
                        if (!existingItems.Contains(item, StringComparer.OrdinalIgnoreCase))
                        {
                            existingItems.Add(item);
                            merged = true;
                        }
                    }

                    if (merged || !File.Exists(savePath))
                    {
                        SaveToJson(savePath, existingItems.OrderBy(x => x).ToList());
                    }

                    return existingItems;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to sync the Fandom category '{category}'. Existing data will be used when available.", ex);
            }
            return result;
        }

        private async Task SaveMonstersDatabaseAsync(List<string> epicMonsters, List<string> largeMonsters)
        {
            try
            {
                var existing = new MonsterDatabase();
                if (File.Exists(_monstersPath))
                {
                    try
                    {
                        string existingJson = await File.ReadAllTextAsync(_monstersPath);
                        var loaded = JsonSerializer.Deserialize<MonsterDatabase>(existingJson);
                        if (loaded != null) existing = loaded;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn("Failed to read the existing monsters database. Newly synchronized entries will be used.");
                        _logger.LogDebug($"Existing monsters database details: {ex.Message}");
                    }
                }

                bool MergeInto(List<string> target, List<string> source)
                {
                    bool changed = false;
                    foreach (var item in source)
                    {
                        if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
                        {
                            target.Add(item);
                            changed = true;
                        }
                    }
                    return changed;
                }

                bool epicChanged = MergeInto(existing.Epic, epicMonsters);
                bool largeChanged = MergeInto(existing.Large, largeMonsters);

                if (epicChanged || largeChanged || !File.Exists(_monstersPath))
                {
                    SaveToJson(_monstersPath, existing);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save the monsters database.", ex);
            }
        }

        private async Task SaveStructuresDatabaseAsync(List<string> newStructures)
        {
            try
            {
                var existing = new List<StructureMapping>();
                if (File.Exists(_structuresPath))
                {
                    try
                    {
                        string existingJson = await File.ReadAllTextAsync(_structuresPath);
                        var loaded = JsonSerializer.Deserialize<List<StructureMapping>>(existingJson);
                        if (loaded != null) existing = loaded;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarn("Failed to read the existing structures database. Newly synchronized entries will be used.");
                        _logger.LogDebug($"Existing structures database details: {ex.Message}");
                    }
                }

                bool changed = false;
                foreach (var name in newStructures)
                {
                    if (!existing.Any(s => s.Keyword.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string targetName = "Turret";
                        if (name.Contains("inhibitor", StringComparison.OrdinalIgnoreCase))
                            targetName = "Inhibitor";
                        else if (name.Contains("nexus", StringComparison.OrdinalIgnoreCase))
                            targetName = "Nexus";

                        existing.Add(new StructureMapping { Keyword = name, TargetName = targetName });
                        changed = true;
                    }
                }

                if (changed || !File.Exists(_structuresPath))
                {
                    SaveToJson(_structuresPath, existing.OrderBy(x => x.Keyword).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save the structures database.", ex);
            }
        }

        private void SaveToJson<T>(string path, T data)
        {
            try
            {
                DirectoriesCreator.CreateParentDirectory(path);
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(temporaryPath, json, Encoding.UTF8);
                    File.Move(temporaryPath, path, true);
                }
                finally
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save synchronized data to '{path}'.", ex);
            }
        }
    }
}
