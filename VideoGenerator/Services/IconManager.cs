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
            // Case 1: Specific Skin Pattern (e.g., CamilleSkin44 or Aatrox_1)
            var skinMatch = Regex.Match(championName, @"^([A-Za-z]+)(?:Skin|_)(\d+)$", RegexOptions.IgnoreCase);
            if (skinMatch.Success)
            {
                string name = skinMatch.Groups[1].Value;
                // Parse as int to strip any leading zeros (e.g., "01" -> "1")
                string skinIndex = int.Parse(skinMatch.Groups[2].Value).ToString();
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
            if (string.IsNullOrEmpty(itemNameOrId)) return null;

            // Resolve name to ID dynamically using DDragon database
            string resolvedId = await _dataFetcher.ResolveItemNameToIdAsync(itemNameOrId);
            if (!string.IsNullOrEmpty(resolvedId))
            {
                itemNameOrId = resolvedId;
            }

            // 1. If numeric (ID), try Community Dragon & DDragon
            if (int.TryParse(itemNameOrId, out int itemId))
            {
                // Try Community Dragon first
                string url = $"https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/assets/items/icons2d/{itemId}.png";
                string path = await _dataFetcher.DownloadIconAsync(url, "item");
                if (!string.IsNullOrEmpty(path)) return path;

                // Fallback to DDragon (using stable version fallback or latest)
                string latestVersion = await _dataFetcher.GetLatestLolVersionAsync();
                string ddragonUrl = $"https://ddragon.leagueoflegends.com/cdn/{latestVersion}/img/item/{itemId}.png";
                path = await _dataFetcher.DownloadIconAsync(ddragonUrl, "item");
                if (!string.IsNullOrEmpty(path)) return path;
            }

            // 2. If it's a name, or if numeric lookup failed, download from Fandom Wiki
            // Convert e.g., "EssenceReaver" or "Essence Reaver" to "Essence_Reaver_item.png"
            string formattedName = itemNameOrId;
            // Add space before capitals if it's CamelCase and doesn't have spaces
            if (!formattedName.Contains(" ") && !formattedName.Contains("_"))
            {
                formattedName = System.Text.RegularExpressions.Regex.Replace(formattedName, @"(?<!^)(?=[A-Z])", " ");
            }
            string wikiFileName = formattedName.Trim().Replace(" ", "_") + "_item.png";

            // Special cases/typos mapping
            if (wikiFileName.Equals("ZhonyasHourglass_item.png", StringComparison.OrdinalIgnoreCase) || 
                wikiFileName.Equals("ZhongyasHourglass_item.png", StringComparison.OrdinalIgnoreCase))
            {
                wikiFileName = "Zhonya%27s_Hourglass_item.png";
            }
            else if (wikiFileName.Equals("LordDominiksRegards_item.png", StringComparison.OrdinalIgnoreCase))
            {
                wikiFileName = "Lord_Dominik%27s_Regards_item.png";
            }

            string fandomUrl = _dataFetcher.GetFandomImageUrl(wikiFileName);
            string localFileName = wikiFileName.Replace("%27", "'");
            string localPath = Path.Combine(AppConfig.IconCacheDir, "item", localFileName);
            
            if (File.Exists(localPath)) return localPath;

            return await _dataFetcher.DownloadIconAsync(fandomUrl, "item", localFileName);
        }

        public async Task<string> GetMonsterIconAsync(string monsterNameFormatted)
        {
            // Build expected filename on Fandom
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

            string localFileName = $"{searchName}Square.png";
            string localPath = Path.Combine(AppConfig.IconCacheDir, "monster", localFileName);
            
            if (File.Exists(localPath))
            {
                return localPath; // Return local cache instantly and skip network call!
            }

            string url = await _dataFetcher.GetMonsterIconUrlAsync(monsterNameFormatted);
            if (url == null) return null;
            return await _dataFetcher.DownloadIconAsync(url, "monster", localFileName);
        }

        public async Task<string> GetStructureIconAsync(string structureNameFormatted)
        {
            if (string.IsNullOrEmpty(structureNameFormatted)) return null;

            string wikiFileName = "Blue_Turret_icon.png";
            if (structureNameFormatted.Contains("Turret", StringComparison.OrdinalIgnoreCase) || 
                structureNameFormatted.Contains("Tower", StringComparison.OrdinalIgnoreCase))
            {
                wikiFileName = "Blue_Turret_icon.png";
            }
            else if (structureNameFormatted.Contains("Inhibitor", StringComparison.OrdinalIgnoreCase))
            {
                wikiFileName = "Blue_Inhibitor_icon.png";
            }
            else if (structureNameFormatted.Contains("Nexus", StringComparison.OrdinalIgnoreCase))
            {
                wikiFileName = "Blue_Nexus_icon.png";
            }

            string localPath = Path.Combine(AppConfig.IconCacheDir, "structure", wikiFileName);
            
            if (File.Exists(localPath)) return localPath;

            string fandomUrl = _dataFetcher.GetFandomImageUrl(wikiFileName);
            return await _dataFetcher.DownloadIconAsync(fandomUrl, "structure", wikiFileName);
        }
    }
}
