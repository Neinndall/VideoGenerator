using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class IconManager
    {
        private readonly DataFetcher _dataFetcher;
        private readonly GroupManager _groupManager;
        private readonly AliasManager _aliasManager;
        private static readonly Random _random = new();

        public IconManager(DataFetcher dataFetcher, GroupManager groupManager, AliasManager aliasManager)
        {
            _dataFetcher = dataFetcher;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
        }

        public async Task<string> GetChampionIconAsync(string championName, string lolVersion)
        {
            // Case 1: Specific Skin Pattern (e.g., CamilleSkin44)
            var skinMatch = Regex.Match(championName, @"(\w+)Skin(\d+)");
            if (skinMatch.Success)
            {
                string name = skinMatch.Groups[1].Value;
                string skinIndex = skinMatch.Groups[2].Value;
                return await GetSplashUrlAsync(name, skinIndex);
            }

            // Case 2: Thematic or Region
            string resolvedUrl = await ResolveThematicOrRegionAsync(championName, lolVersion);
            if (resolvedUrl != null) return resolvedUrl;

            // Case 3: Base Champion (using AliasManager)
            string internalName = _aliasManager.GetInternalName(championName);

            string url = $"https://ddragon.leagueoflegends.com/cdn/{lolVersion}/img/champion/{internalName}.png";
            return await _dataFetcher.DownloadIconAsync(url, "champion");
        }

        private async Task<string> ResolveThematicOrRegionAsync(string target, string lolVersion)
        {
            // Check dynamic groups from GroupManager
            var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matchedGroup != null)
            {
                var candidates = matchedGroup.GetChampionsList();
                if (candidates.Count > 0)
                {
                    string randomChamp = candidates[_random.Next(candidates.Count)];
                    return await GetChampionIconAsync(randomChamp, lolVersion);
                }
            }

            // Advanced Thematic Search (skinlines.json + skins.json)
            try
            {
                var skinLines = await _dataFetcher.GetSkinLinesAsync();
                var matchingLines = skinLines.Where(sl => 
                    sl.GetProperty("name").GetString().Contains(target, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchingLines.Count > 0)
                {
                    var lineIds = matchingLines.Select(ml => ml.GetProperty("id").GetInt32()).ToList();
                    var allSkins = await _dataFetcher.GetSkinsDataAsync();

                    var thematicSkins = allSkins.Values.Where(skin => 
                        skin.TryGetProperty("skinLines", out var slProp) && 
                        slProp.ValueKind == JsonValueKind.Array &&
                        slProp.EnumerateArray().Any(idObj => lineIds.Contains(idObj.GetProperty("id").GetInt32()))
                    ).ToList();

                    if (thematicSkins.Count > 0)
                    {
                        var chosenSkin = thematicSkins[_random.Next(thematicSkins.Count)];
                        int id = chosenSkin.GetProperty("id").GetInt32();
                        string splashPath = chosenSkin.GetProperty("splashPath").GetString();
                        
                        string skinIndex = (id % 1000).ToString();
                        
                        // Extract Champion internal name from splashPath
                        var nameMatch = Regex.Match(splashPath, @"Characters/([^/]+)/");
                        string champInternalName = nameMatch.Success ? nameMatch.Groups[1].Value : "";

                        return await GetSplashUrlAsync(champInternalName, skinIndex);
                    }
                }
            }
            catch { }

            return null;
        }

        private async Task<string> GetSplashUrlAsync(string championName, string skinIndex)
        {
            string internalName = _aliasManager.GetInternalName(championName);
            string url = $"https://ddragon.leagueoflegends.com/cdn/img/champion/splash/{internalName}_{skinIndex}.jpg";
            return await _dataFetcher.DownloadIconAsync(url, "splash");
        }

        public async Task<string> GetItemIconAsync(string itemNameOrId)
        {
            if (int.TryParse(itemNameOrId, out int itemId))
            {
                string url = $"https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/assets/items/icons2d/{itemId}.png";
                return await _dataFetcher.DownloadIconAsync(url, "item");
            }
            return null;
        }

        public async Task<string> GetMonsterIconAsync(string monsterNameFormatted)
        {
            string url = await _dataFetcher.GetMonsterIconUrlAsync(monsterNameFormatted);
            if (url == null) return null;
            return await _dataFetcher.DownloadIconAsync(url, "monster");
        }
    }
}
