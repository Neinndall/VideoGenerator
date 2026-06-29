using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class DatabaseBuilder
    {
        private readonly HttpClient _httpClient;

        public DatabaseBuilder(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task InitializeDatabasesAsync(string currentLolVersion)
        {
            bool versionChanged = CheckIfVersionChanged(currentLolVersion);

            // 1. Sync Champions and Items from DDragon if version changed or files missing
            if (versionChanged || !File.Exists(AppConfig.ChampionsPath))
            {
                await SyncChampionsAsync(currentLolVersion);
            }

            if (versionChanged || !File.Exists(AppConfig.ItemsPath))
            {
                await SyncItemsAsync(currentLolVersion);
            }

            // 2. Sync CommunityDragon data (skins, skinlines, full items database)
            string defaultLocale = AppConfig.GetCdragonLocale();
            await Task.WhenAll(
                SyncCommunityDragonJsonAsync(AppConfig.GetSkinsDataUrl(defaultLocale), AppConfig.GetSkinsCachePath(defaultLocale)),
                SyncCommunityDragonJsonAsync(AppConfig.GetSkinLinesUrl(defaultLocale), AppConfig.GetSkinLinesCachePath(defaultLocale)),
                SyncCommunityDragonJsonAsync(AppConfig.GetItemsDataUrl(defaultLocale), AppConfig.GetItemsCachePath(defaultLocale))
            );

            // 3. Fandom sync (Monsters/Structures) - These are lore-based, so we always try to merge new ones
            var epicMonsters = await SyncFandomCategoryAsync("Epic_monsters");
            var largeMonsters = await SyncFandomCategoryAsync("Large_monsters");
            await SaveMonstersDatabaseAsync(epicMonsters, largeMonsters);
            await SyncFandomCategoryAsync("Structures", AppConfig.StructuresPath);

            // 4. Save the new version locally if sync was successful
            if (versionChanged)
            {
                SaveLocalVersion(currentLolVersion);
            }
        }

        private async Task SyncCommunityDragonJsonAsync(string url, string cachePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

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
                    await File.WriteAllTextAsync(cachePath, json, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing CommunityDragon data from {url}: {ex.Message}");
            }
        }

        private bool CheckIfVersionChanged(string currentVersion)
        {
            try
            {
                if (!File.Exists(AppConfig.LocalVersionPath)) return true;
                string localVersion = File.ReadAllText(AppConfig.LocalVersionPath).Trim();
                return !localVersion.Equals(currentVersion, StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

        private void SaveLocalVersion(string version)
        {
            try
            {
                File.WriteAllText(AppConfig.LocalVersionPath, version, Encoding.UTF8);
            }
            catch { }
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

                    SaveToJson(AppConfig.ChampionsPath, championsList.OrderBy(x => x).ToList());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing champions: {ex.Message}");
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

                    SaveToJson(AppConfig.ItemsPath, itemsDict);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing items: {ex.Message}");
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
                        catch { }
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
                Console.WriteLine($"Error syncing Fandom category {category}: {ex.Message}");
            }
            return result;
        }

        private async Task SaveMonstersDatabaseAsync(List<string> epicMonsters, List<string> largeMonsters)
        {
            try
            {
                var existing = new MonsterDatabase();
                if (File.Exists(AppConfig.MonstersPath))
                {
                    try
                    {
                        string existingJson = await File.ReadAllTextAsync(AppConfig.MonstersPath);
                        var loaded = JsonSerializer.Deserialize<MonsterDatabase>(existingJson);
                        if (loaded != null) existing = loaded;
                    }
                    catch { }
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

                if (epicChanged || largeChanged || !File.Exists(AppConfig.MonstersPath))
                {
                    SaveToJson(AppConfig.MonstersPath, existing);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving monsters database: {ex.Message}");
            }
        }

        private void SaveToJson<T>(string path, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch { }
        }
    }
}
