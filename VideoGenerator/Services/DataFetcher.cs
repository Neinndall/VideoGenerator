using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
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
                if (File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cachedJson);
                    return _skinsCache ?? new Dictionary<string, JsonElement>();
                }

                var response = await _httpClient.GetStringAsync(AppConfig.SkinsDataUrl);
                await File.WriteAllTextAsync(cachePath, response);
                
                _skinsCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);
                return _skinsCache ?? new Dictionary<string, JsonElement>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting skins data: {ex.Message}");
                return new Dictionary<string, JsonElement>();
            }
        }

        public async Task<List<JsonElement>> GetSkinLinesAsync()
        {
            if (_skinLinesCache != null) return _skinLinesCache;

            try
            {
                string cachePath = Path.Combine(AppConfig.CacheDir, "skinlines_data.json");
                if (File.Exists(cachePath))
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(cachedJson);
                    return _skinLinesCache ?? new List<JsonElement>();
                }

                var response = await _httpClient.GetStringAsync(AppConfig.SkinLinesUrl);
                await File.WriteAllTextAsync(cachePath, response);
                
                _skinLinesCache = JsonSerializer.Deserialize<List<JsonElement>>(response);
                return _skinLinesCache ?? new List<JsonElement>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting skinlines data: {ex.Message}");
                return new List<JsonElement>();
            }
        }

        public async Task<string> DownloadIconAsync(string url, string category)
        {
            try
            {
                string categoryDir = Path.Combine(AppConfig.IconCacheDir, category);
                Directory.CreateDirectory(categoryDir);
                
                string fileName = Path.GetFileName(new Uri(url).LocalPath);
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

        public async Task<string> GetMonsterIconUrlAsync(string monsterNameFormatted)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(AppConfig.MonsterWikiUrl);
                
                List<string> searchNames = new List<string> { monsterNameFormatted.Replace(" ", "_") };
                
                if (monsterNameFormatted.Contains("Dragon")) searchNames.Add("Elder_Dragon");
                if (monsterNameFormatted.Contains("Baron")) searchNames.Add("Baron_Nashor");
                if (monsterNameFormatted.Contains("Sentinel")) searchNames.Add("Blue_Buff");
                if (monsterNameFormatted.Contains("Brambleback")) searchNames.Add("Red_Buff");

                foreach (var pName in searchNames.Distinct())
                {
                    var srcsetMatch = Regex.Match(html, $@"srcset=""([^""]*{Regex.Escape(pName)}Square\.png/[^""\s]+)\s\dx""", RegexOptions.IgnoreCase);
                    if (srcsetMatch.Success)
                    {
                        return "https://wiki.leagueoflegends.com" + srcsetMatch.Groups[1].Value;
                    }
                    
                    var srcMatch = Regex.Match(html, $@"src=""([^""]*{Regex.Escape(pName)}Square\.png)""", RegexOptions.IgnoreCase);
                    if (srcMatch.Success)
                    {
                        return "https://wiki.leagueoflegends.com" + srcMatch.Groups[1].Value;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
