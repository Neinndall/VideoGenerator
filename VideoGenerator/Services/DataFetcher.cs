using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    public class DataFetcher
    {
        private readonly HttpClient _httpClient;
        private Dictionary<string, JsonElement> _skinsCache;
        private List<JsonElement> _skinLinesCache;

        public DataFetcher()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public async Task<string> GetLatestLolVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(AppConfig.VersionsUrl);
                var versions = JsonSerializer.Deserialize<List<string>>(response);
                return versions?.FirstOrDefault() ?? "14.1.1";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting LoL version: {ex.Message}");
                return "14.1.1";
            }
        }

        public async Task<Dictionary<string, JsonElement>> GetSkinsDataAsync()
        {
            if (_skinsCache != null) return _skinsCache;

            try
            {
                string cachePath = Path.Combine(AppConfig.CacheDir, "skins_data.json");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

                var request = new HttpRequestMessage(HttpMethod.Get, AppConfig.SkinsDataUrl);
                if (File.Exists(cachePath))
                {
                    request.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(cachePath);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    await File.WriteAllTextAsync(cachePath, json);
                    _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }

                if (File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }

                return new Dictionary<string, JsonElement>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting skins data: {ex.Message}");
                try
                {
                    string cachePath = Path.Combine(AppConfig.CacheDir, "skins_data.json");
                    if (File.Exists(cachePath))
                    {
                        string cachedJson = await File.ReadAllTextAsync(cachePath);
                        _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                        return _skinsCache ?? new Dictionary<string, JsonElement>();
                    }
                }
                catch { }
                return new Dictionary<string, JsonElement>();
            }
        }

        public async Task<List<JsonElement>> GetSkinLinesAsync()
        {
            if (_skinLinesCache != null) return _skinLinesCache;

            try
            {
                string cachePath = Path.Combine(AppConfig.CacheDir, "skinlines_data.json");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

                var request = new HttpRequestMessage(HttpMethod.Get, AppConfig.SkinLinesUrl);
                if (File.Exists(cachePath))
                {
                    request.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(cachePath);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    return _skinLinesCache ?? new List<JsonElement>();
                }

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    await File.WriteAllTextAsync(cachePath, json);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(json);
                    return _skinLinesCache ?? new List<JsonElement>();
                }

                if (File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    return _skinLinesCache ?? new List<JsonElement>();
                }

                return new List<JsonElement>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting skinlines data: {ex.Message}");
                try
                {
                    string cachePath = Path.Combine(AppConfig.CacheDir, "skinlines_data.json");
                    if (File.Exists(cachePath))
                    {
                        string cachedJson = await File.ReadAllTextAsync(cachePath);
                        _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                        return _skinLinesCache ?? new List<JsonElement>();
                    }
                }
                catch { }
                return new List<JsonElement>();
            }
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
            string cleanName = filename.Replace(" ", "_");
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(cleanName);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 2; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                string hex = sb.ToString();
                char a = hex[0];
                string ab = hex.Substring(0, 2);
                
                return $"https://static.wikia.nocookie.net/leagueoflegends/images/{a}/{ab}/{cleanName}";
            }
        }

        public async Task<string> GetMonsterIconUrlAsync(string monsterNameFormatted)
        {
            // Replaced HTML scraping with direct, robust Fandom CDN URL generation via MD5 hashing
            string searchName = monsterNameFormatted.Replace(" ", "_");
            
            if (monsterNameFormatted.Contains("Cloud", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Cloud_Drake";
            else if (monsterNameFormatted.Contains("Hextech", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Hextech_Drake";
            else if (monsterNameFormatted.Contains("Infernal", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Infernal_Drake";
            else if (monsterNameFormatted.Contains("Mountain", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Mountain_Drake";
            else if (monsterNameFormatted.Contains("Ocean", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Ocean_Drake";
            else if (monsterNameFormatted.Contains("Chemtech", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Chemtech_Drake";
            else if (monsterNameFormatted.Contains("Elder", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Elder_Dragon";
            else if (monsterNameFormatted.Contains("Dragon", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Dragon";
            else if (monsterNameFormatted.Contains("Baron", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Baron_Nashor";
            else if (monsterNameFormatted.Contains("Herald", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Rift_Herald";
            else if (monsterNameFormatted.Contains("Sentinel", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Blue", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Blue_Sentinel";
            else if (monsterNameFormatted.Contains("Brambleback", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Red", StringComparison.OrdinalIgnoreCase)) 
                searchName = "Red_Brambleback";
            else if (monsterNameFormatted.Contains("Voidgrub", StringComparison.OrdinalIgnoreCase))
                searchName = "Voidgrub";
            else if (monsterNameFormatted.Contains("Scuttle", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Crab", StringComparison.OrdinalIgnoreCase))
                searchName = "Rift_Scuttler";
            else if (monsterNameFormatted.Contains("Krug", StringComparison.OrdinalIgnoreCase))
                searchName = "Ancient_Krug";
            else if (monsterNameFormatted.Contains("Wolf", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Wolves", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Murkwolf", StringComparison.OrdinalIgnoreCase))
                searchName = "Greater_Murkwolf";
            else if (monsterNameFormatted.Contains("Raptor", StringComparison.OrdinalIgnoreCase) || 
                     monsterNameFormatted.Contains("Raptors", StringComparison.OrdinalIgnoreCase))
                searchName = "Crimson_Raptor";
            else if (monsterNameFormatted.Contains("Gromp", StringComparison.OrdinalIgnoreCase))
                searchName = "Gromp";
            else if (monsterNameFormatted.Contains("Vilemaw", StringComparison.OrdinalIgnoreCase))
                searchName = "Vilemaw";

            string fileName = $"{searchName}Square.png";
            return await Task.FromResult(GetFandomImageUrl(fileName));
        }
    }
}
