using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public class SkinInteractionParser : IEventParser
    {
        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;
        private readonly AliasManager _aliasManager;

        public SkinInteractionParser(
            TranslationService translationService,
            DataFetcher dataFetcher,
            AliasManager aliasManager)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            _aliasManager = aliasManager;
        }

        public bool CanParse(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return false;
            return folderName.Contains("Skin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string workingFolder = StripOwnerPrefix(folderName);
            // Remove 2D / 3D tokens to prevent them from corrupting champion names
            workingFolder = Regex.Replace(workingFolder, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            workingFolder = Regex.Replace(workingFolder, @"2D|3D", "", RegexOptions.IgnoreCase);

            var skinMatches = Regex.Matches(workingFolder, @"([A-Za-z]+)Skin(\d+)");
            if (skinMatches.Count == 0) return null;

            List<string> processedChampions = new();
            string lastChampionWithSkin = "General";

            foreach (Match match in skinMatches)
            {
                string rawChampionName = match.Groups[1].Value;
                int skinId = int.Parse(match.Groups[2].Value);

                string actualChampionName = rawChampionName;
                string[] prefixes = { "Kill", "FirstEncounter", "SecondEncounter", "MoveFirstAlly", "Move", "Assist", "AttackNear", "Death" };
                foreach (var prefix in prefixes)
                {
                    if (actualChampionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        actualChampionName = actualChampionName.Substring(prefix.Length);
                        break;
                    }
                }

                // Smart Fallback: if not valid, extract by checking if it ends with a valid champion name or alias
                if (!_aliasManager.IsValidChampion(actualChampionName))
                {
                    for (int len = 1; len <= actualChampionName.Length; len++)
                    {
                        string candidate = actualChampionName.Substring(actualChampionName.Length - len);
                        if (_aliasManager.IsValidChampion(candidate))
                        {
                            actualChampionName = candidate;
                            break;
                        }
                    }
                }

                // Clean the champion name through AliasManager
                actualChampionName = _aliasManager.GetInternalName(actualChampionName);

                string skinName = await FindSkinNameAsync(actualChampionName, skinId);
                if (skinName != null)
                {
                    string displayName = GetDisplaySkinName(actualChampionName, skinName, out string officialChampionName);
                    processedChampions.Add(!string.IsNullOrEmpty(displayName)
                        ? displayName
                        : (officialChampionName ?? actualChampionName));
                }
                else
                {
                    processedChampions.Add($"{actualChampionName} (Skin {skinId})");
                }
                lastChampionWithSkin = $"{actualChampionName}Skin{skinId}";
            }

            string key;
            string iconType = "champion";
            string iconLookup = lastChampionWithSkin;

            if (folderName.Contains("Kill", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_kill_one" : "interaction_kill_two";
            }
            else if (folderName.Contains("Death", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_death_one" : "interaction_death_two";
            }
            else if (folderName.Contains("Assist", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_assist_one";
            }
            else if (folderName.Contains("FirstEncounter", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_first_encounter_one" : "interaction_first_encounter_two";
            }
            else if (folderName.Contains("AttackNear", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_attack_near_one";
            }
            else if (folderName.Contains("Buff", StringComparison.OrdinalIgnoreCase) || folderName.Contains("Receive", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_buff_receive";
            }
            else if (folderName.Contains("Ally", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_move_first_ally";
            }
            else if (folderName.Contains("Enemy", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_move_first_enemy";
            }
            else
            {
                key = processedChampions.Count == 1 ? "interaction_generic_one" : "interaction_generic_two";
                // Keep the champion skin icon instead of falling back to generic!
                iconType = "champion";
                iconLookup = lastChampionWithSkin;
            }

            string displayText = _translationService.GetText(language, key, processedChampions.Cast<object>().ToArray());

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconLookup,
                IconType = iconType
            };
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            var prefixMatch = Regex.Match(folderName, @"^(Play_vo_|Play_)([A-Za-z0-9]+?)(Skin\d+)?_", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                return folderName.Substring(prefixMatch.Length);
            }
            return folderName;
        }

        private async Task<string> FindSkinNameAsync(string championName, int skinId)
        {
            var skinsData = await _dataFetcher.GetSkinsDataAsync();
            if (skinsData == null) return null;

            string Normalize(string input) => input?.Replace("'", "").Replace(" ", "").Replace("_", "").ToLowerInvariant() ?? "";
            string normalizedChampion = Normalize(championName);

            foreach (var skin in skinsData.Values)
            {
                string splashPath = skin.TryGetProperty("splashPath", out var splashProp) ? splashProp.GetString() ?? "" : "";
                var match = Regex.Match(splashPath, @"Skin(\d+)");
                if (!match.Success || int.Parse(match.Groups[1].Value) != skinId) continue;

                // Primary: splash path contains the champion internal name (e.g. /Belveth/Skins/Skin19/)
                if (splashPath.Contains(championName, StringComparison.OrdinalIgnoreCase))
                {
                    return skin.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                }

                // Fallback: normalize both names to handle apostrophes / spaces (e.g. Bel'Veth vs Belveth)
                string skinName = skin.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() ?? "" : "";
                if (Normalize(skinName).Contains(normalizedChampion))
                {
                    return skinName;
                }
            }
            return null;
        }

        private string GetDisplaySkinName(string championName, string fullSkinName, out string officialChampionName)
        {
            officialChampionName = null;
            string name = fullSkinName;
            if (name.StartsWith("After Hours ", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("After Hours ".Length).Trim();
            }

            string Normalize(string input) => input?.Replace("'", "").Replace(" ", "").ToLowerInvariant() ?? "";

            // Base skin: the skin name is just the champion's official name (e.g. "Bel'Veth")
            if (Normalize(name) == Normalize(championName))
            {
                officialChampionName = name;
                return "";
            }

            // Special case mappings for weird skin naming
            if (name.Equals("Spirit Blossom Springs Sett", StringComparison.OrdinalIgnoreCase))
                return "Springs Spirit Blossom Sett";

            // Return the full skin name as-is (e.g. "Primordian Bel'Veth")
            return name;
        }
    }
}
