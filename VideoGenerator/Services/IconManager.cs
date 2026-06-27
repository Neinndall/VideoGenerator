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
        private readonly SkinlineManager _skinlineManager;
        private static readonly Random _random = new();

        public IconManager(DataFetcher dataFetcher, GroupManager groupManager, AliasManager aliasManager, SkinlineManager skinlineManager)
        {
            _dataFetcher = dataFetcher;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _skinlineManager = skinlineManager;
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
                return await GetTileUrlAsync(name, skinIndex);
            }

            // Case 2: Thematic or Region
            string resolvedUrl = await ResolveThematicOrRegionAsync(championName, lolVersion);
            if (resolvedUrl != null) return resolvedUrl;

            // Case 3: Base Champion (using AliasManager)
            string internalName = _aliasManager.GetInternalName(championName);

            string url = $"https://ddragon.leagueoflegends.com/cdn/{lolVersion}/img/champion/{internalName}.png";
            return await _dataFetcher.DownloadIconAsync(url, "champion");
        }

        private string GetRegionCrestFileName(string regionName)
        {
            string name = regionName.Trim();
            if (name.Equals("Darkin", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Ascended Darkin", StringComparison.OrdinalIgnoreCase))
            {
                return "Darkin_icon.png";
            }
            if (name.Equals("Vastaya", StringComparison.OrdinalIgnoreCase))
            {
                return "Vastaya_icon.png";
            }
            if (name.Equals("Demon", StringComparison.OrdinalIgnoreCase))
            {
                return "Demon_icon.png";
            }
            if (name.Equals("Ascended", StringComparison.OrdinalIgnoreCase))
            {
                return "Shurima_Crest_icon.png";
            }
            if (name.Equals("Kinkou", StringComparison.OrdinalIgnoreCase))
            {
                return "Ionia_Crest_icon.png";
            }
            if (name.Equals("Lunari", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Solari", StringComparison.OrdinalIgnoreCase))
            {
                return "Targon_Crest_icon.png";
            }

            return $"{name}_Crest_icon.png".Replace(" ", "_");
        }

        private async Task<string> ResolveThematicOrRegionAsync(string target, string lolVersion)
        {
            // Check dynamic groups from GroupManager
            var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matchedGroup != null)
            {
                if (matchedGroup.Category.Equals("Region", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = GetRegionCrestFileName(matchedGroup.Name);
                    string localPath = Path.Combine(AppConfig.IconCacheDir, "region", fileName);
                    if (File.Exists(localPath)) return localPath;

                    string fandomUrl = await _dataFetcher.ResolveFandomImageUrlAsync(fileName);
                    if (!string.IsNullOrEmpty(fandomUrl))
                    {
                        string downloaded = await _dataFetcher.DownloadIconAsync(fandomUrl, "region", fileName);
                        if (!string.IsNullOrEmpty(downloaded)) return downloaded;
                    }
                }

                var candidates = matchedGroup.GetChampionsList();
                if (candidates.Count > 0)
                {
                    string randomChamp = candidates[_random.Next(candidates.Count)];
                    return await GetChampionIconAsync(randomChamp, lolVersion);
                }
            }

            // Advanced Thematic Search via SkinlineManager
            try
            {
                if (_skinlineManager.IsKnownSkinline(target))
                {
                    var candidates = _skinlineManager.GetChampionsWithSkin(target);
                    if (candidates.Count > 0)
                    {
                        var chosen = candidates[_random.Next(candidates.Count)];
                        return await GetChampionIconAsync(chosen.ToString(), lolVersion);
                    }
                }
            }
            catch { }

            return null;
        }

        private async Task<string> GetTileUrlAsync(string championName, string skinIndex)
        {
            string internalName = _aliasManager.GetInternalName(championName);
            string url = $"https://ddragon.leagueoflegends.com/cdn/img/champion/tiles/{internalName}_{skinIndex}.jpg";
            return await _dataFetcher.DownloadIconAsync(url, "tiles");
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
            // Order of attempts based on Wiki conventions: _item.png -> .png -> _icon.png
            string formattedName = itemNameOrId;
            if (!formattedName.Contains(" ") && !formattedName.Contains("_"))
            {
                formattedName = System.Text.RegularExpressions.Regex.Replace(formattedName, @"(?<!^)(?=[A-Z])", " ");
            }
            string baseWikiName = formattedName.Trim().Replace(" ", "_");

            // List of naming patterns to try on the Wiki
            string[] patterns = { $"{baseWikiName}_item.png", $"{baseWikiName}.png", $"{baseWikiName}_icon.png", $"{baseWikiName}_ping.png" };

            foreach (var wikiFileName in patterns)
            {
                // Special mapping for common outliers/typos (preserve this logic)
                string finalFileName = wikiFileName;
                if (finalFileName.Equals("Zhonyas_Hourglass_item.png", StringComparison.OrdinalIgnoreCase)) finalFileName = "Zhonya%27s_Hourglass_item.png";
                if (finalFileName.Equals("Lord_Dominiks_Regards_item.png", StringComparison.OrdinalIgnoreCase)) finalFileName = "Lord_Dominik%27s_Regards_item.png";

                string localFileName = finalFileName.Replace("%27", "'");
                string localPath = Path.Combine(AppConfig.IconCacheDir, "item", localFileName);
                if (File.Exists(localPath)) return localPath;

                string fandomUrl = await _dataFetcher.ResolveFandomImageUrlAsync(finalFileName);
                if (string.IsNullOrEmpty(fandomUrl)) continue;

                string downloaded = await _dataFetcher.DownloadIconAsync(fandomUrl, "item", localFileName);
                
                if (!string.IsNullOrEmpty(downloaded)) return downloaded;
            }

            return null;
        }

        public async Task<string> GetMonsterIconAsync(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName)) return null;

            var monsterDb = _dataFetcher.LoadMonsterDatabase();
            string resolvedName = ResolveMonsterName(monsterName, monsterDb);
            if (string.IsNullOrEmpty(resolvedName)) return null;

            string searchName = resolvedName.Replace(" ", "_");

            // Order of attempts for monster icons
            string[] patterns = { 
                $"{searchName}Square.png",
                $"{searchName}_Square.png",
                $"{searchName}.png"
            };

            foreach (var wikiFileName in patterns)
            {
                string localPath = Path.Combine(AppConfig.IconCacheDir, "monster", wikiFileName);
                if (File.Exists(localPath)) return localPath;

                string fandomUrl = await _dataFetcher.ResolveFandomImageUrlAsync(wikiFileName);
                if (string.IsNullOrEmpty(fandomUrl)) continue;

                string downloaded = await _dataFetcher.DownloadIconAsync(fandomUrl, "monster", wikiFileName);
                
                if (!string.IsNullOrEmpty(downloaded)) return downloaded;
            }

            return null;
        }

        private string ResolveMonsterName(string monsterName, MonsterDatabase db)
        {
            string normalized = monsterName;
            if (!normalized.Contains(" ") && !normalized.Contains("_"))
            {
                normalized = Regex.Replace(normalized, @"(?<!^)(?=[A-Z])", " ");
            }
            string cleanTarget = normalized.Trim();

            var allMonsters = db.All;
            if (allMonsters.Count == 0) return cleanTarget;

            // 1. Exact match
            var exact = allMonsters.FirstOrDefault(m => m.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 1.5 Special handling for generic "Dragon" / "Drake" targets to avoid resolving to Elder Dragon.
            // The Fandom wiki provides a generic DragonSquare.png asset, so keep the lookup name as "Dragon"
            // instead of falling back to the only substring match ("Elder Dragon").
            if (cleanTarget.Equals("Dragon", StringComparison.OrdinalIgnoreCase) ||
                cleanTarget.Equals("Drake", StringComparison.OrdinalIgnoreCase))
            {
                return "Dragon";
            }

            // 2. Substring matches (prefer longest = most specific)
            var matches = allMonsters
                .Where(m => cleanTarget.Contains(m, StringComparison.OrdinalIgnoreCase) ||
                            m.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Length)
                .ToList();

            if (matches.Count > 0) return matches[0];

            // 3. Generic category targets: pick a representative from the category
            bool isEpic = cleanTarget.Contains("Epic", StringComparison.OrdinalIgnoreCase);
            bool isLarge = cleanTarget.Contains("Large", StringComparison.OrdinalIgnoreCase) ||
                           cleanTarget.Contains("Monster", StringComparison.OrdinalIgnoreCase);

            if (isEpic && db.Epic.Count > 0)
            {
                // Prefer Baron as the most iconic epic monster, fallback to first
                var baron = db.Epic.FirstOrDefault(m => m.Contains("Baron", StringComparison.OrdinalIgnoreCase));
                return baron ?? db.Epic[0];
            }

            if (isLarge && db.Large.Count > 0)
            {
                return db.Large[0];
            }

            return cleanTarget;
        }

        public async Task<string> GetStructureIconAsync(string structureName)
        {
            if (string.IsNullOrEmpty(structureName)) return null;

            string formattedName = structureName;
            if (!formattedName.Contains(" ") && !formattedName.Contains("_"))
            {
                formattedName = Regex.Replace(formattedName, @"(?<!^)(?=[A-Z])", " ");
            }
            string baseWikiName = formattedName.Trim().Replace(" ", "_");

            // Blue Turret is the default for structures
            if (baseWikiName.Contains("Turret") || baseWikiName.Contains("Tower")) baseWikiName = "Blue_Turret";
            else if (baseWikiName.Contains("Inhibitor")) baseWikiName = "Blue_Inhibitor";
            else if (baseWikiName.Contains("Nexus")) baseWikiName = "Blue_Nexus";

            string[] patterns = { 
                $"{baseWikiName}_icon.png",
                $"{baseWikiName}.png"
            };

            foreach (var wikiFileName in patterns)
            {
                string localPath = Path.Combine(AppConfig.IconCacheDir, "structure", wikiFileName);
                if (File.Exists(localPath)) return localPath;

                string fandomUrl = await _dataFetcher.ResolveFandomImageUrlAsync(wikiFileName);
                if (string.IsNullOrEmpty(fandomUrl)) continue;

                string downloaded = await _dataFetcher.DownloadIconAsync(fandomUrl, "structure", wikiFileName);
                
                if (!string.IsNullOrEmpty(downloaded)) return downloaded;
            }

            return null;
        }

        public async Task<string> GetSystemIconAsync(string systemName)
        {
            if (string.IsNullOrEmpty(systemName)) return null;

            string formattedName = systemName;
            if (!formattedName.Contains(" ") && !formattedName.Contains("_"))
            {
                formattedName = Regex.Replace(formattedName, @"(?<!^)(?=[A-Z])", " ");
            }

            string baseWikiNameSpaces = formattedName.Trim().Replace("_", " ");
            string baseWikiNameUnderscores = formattedName.Trim().Replace(" ", "_");

            var candidates = new List<string>
            {
                $"{baseWikiNameSpaces} ping.png",
                $"{baseWikiNameUnderscores} ping.png",
                $"{baseWikiNameUnderscores}_ping.png",
                $"{baseWikiNameSpaces} icon.png",
                $"{baseWikiNameUnderscores}_icon.png",
                $"{baseWikiNameSpaces}.png",
                $"{baseWikiNameUnderscores}.png",
                $"{baseWikiNameUnderscores}_item.png"
            };

            // Special case for Gold and Danger/Caution
            if (systemName.Equals("Gold", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Insert(0, "Gold_icon.png");
            }
            else if (systemName.Equals("Danger", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Insert(0, "Retreat ping.png");
            }

            foreach (var wikiFileName in candidates)
            {
                string localPath = Path.Combine(AppConfig.IconCacheDir, "system", wikiFileName);
                if (File.Exists(localPath)) return localPath;

                string fandomUrl = await _dataFetcher.ResolveFandomImageUrlAsync(wikiFileName);
                if (string.IsNullOrEmpty(fandomUrl)) continue;

                string downloaded = await _dataFetcher.DownloadIconAsync(fandomUrl, "system", wikiFileName);
                
                if (!string.IsNullOrEmpty(downloaded)) return downloaded;
            }

            return null;
        }
    }
}
