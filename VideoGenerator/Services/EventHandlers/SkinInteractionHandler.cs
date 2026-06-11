using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class SkinInteractionHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;

        public SkinInteractionHandler(TranslationService translationService, DataFetcher dataFetcher)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
        }

        public bool CanHandle(string folderName)
        {
            return folderName.Contains("Skin");
        }

        private async Task<string> FindSkinNameAsync(string championName, int skinId)
        {
            var skinsData = await _dataFetcher.GetSkinsDataAsync();
            if (skinsData == null) return null;

            foreach (var skin in skinsData.Values)
            {
                string skinName = skin.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (skinName.Contains(championName, StringComparison.OrdinalIgnoreCase))
                {
                    string splashPath = skin.TryGetProperty("splashPath", out var splashProp) ? splashProp.GetString() ?? "" : "";
                    var match = Regex.Match(splashPath, @"Skin(\d+)");
                    if (match.Success && int.Parse(match.Groups[1].Value) == skinId)
                    {
                        return skinName;
                    }
                }
            }
            return null;
        }

        private string GetDisplaySkinName(string championName, string fullSkinName)
        {
            if (fullSkinName.EndsWith(championName, StringComparison.OrdinalIgnoreCase))
            {
                string themeName = fullSkinName.Substring(0, fullSkinName.Length - championName.Length).Trim();
                if (string.Equals(themeName, "base", StringComparison.OrdinalIgnoreCase)) return "";
                if (themeName == "Spirit Blossom Springs") return "Springs Spirit Blossom";
                return themeName;
            }

            if (string.Equals(fullSkinName, championName, StringComparison.OrdinalIgnoreCase)) return "";
            return fullSkinName;
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            var skinMatches = Regex.Matches(folderName, @"(\w+)Skin(\d+)");
            if (skinMatches.Count == 0) return new ParsedEvent { OriginalFolder = folderName };

            List<string> processedChampions = new();
            string lastChampionName = "General";

            foreach (Match match in skinMatches)
            {
                string rawChampionName = match.Groups[1].Value;
                int skinId = int.Parse(match.Groups[2].Value);

                string actualChampionName = rawChampionName;
                string[] prefixes = { "Kill", "FirstEncounter", "SecondEncounter", "MoveFirstAlly", "Move", "Assist", "AttackNear" };
                foreach (var prefix in prefixes)
                {
                    if (actualChampionName.StartsWith(prefix))
                    {
                        actualChampionName = actualChampionName.Substring(prefix.Length);
                        break;
                    }
                }

                string skinName = await FindSkinNameAsync(actualChampionName, skinId);
                if (skinName != null)
                {
                    string displaySkinTheme = GetDisplaySkinName(actualChampionName, skinName);
                    processedChampions.Add(!string.IsNullOrEmpty(displaySkinTheme) 
                        ? $"{actualChampionName} {displaySkinTheme}" 
                        : actualChampionName);
                }
                else
                {
                    processedChampions.Add($"{actualChampionName} (Skin {skinId})");
                }
                lastChampionName = actualChampionName;
            }

            string key;
            if (folderName.Contains("Kill"))
            {
                key = processedChampions.Count == 1 ? "interaction_kill_one" : "interaction_kill_two";
            }
            else if (folderName.Contains("Death"))
            {
                key = processedChampions.Count == 1 ? "interaction_death_one" : "interaction_death_two";
            }
            else if (folderName.Contains("Assist"))
            {
                key = "interaction_assist_one";
            }
            else if (folderName.Contains("FirstEncounter"))
            {
                key = processedChampions.Count == 1 ? "interaction_first_encounter_one" : "interaction_first_encounter_two";
            }
            else
            {
                key = processedChampions.Count == 1 ? "interaction_generic_one" : "interaction_generic_two";
            }

            string displayText = _translationService.GetText(language, key, processedChampions.Cast<object>().ToArray());

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = lastChampionName,
                IconType = "champion"
            };
        }
    }
}
