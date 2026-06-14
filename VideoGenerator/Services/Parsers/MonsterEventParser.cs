using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public class MonsterEventParser : IEventParser
    {
        private readonly TranslationService _translationService;

        public MonsterEventParser(TranslationService translationService)
        {
            _translationService = translationService;
        }

        public bool CanParse(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return false;
            string targetPart = StripOwnerPrefix(folderName);
            return folderName.Contains("Attack2D", StringComparison.OrdinalIgnoreCase) && 
                  !targetPart.Contains("Skin", StringComparison.OrdinalIgnoreCase);
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

        public Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string cleanTarget = StripOwnerPrefix(folderName);
            var match = Regex.Match(cleanTarget, @"Attack2D_?([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
            if (!match.Success) return Task.FromResult<ParsedEvent>(null);

            string monsterNameRaw = match.Groups[1].Value.Trim('_');

            if (monsterNameRaw.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                string actionText = _translationService.GetText(language, "event_attack");
                string suffixText = _translationService.GetText(language, "suffix_in_general");
                string generalDisplayText = $"{actionText}{suffixText}";

                return Task.FromResult(new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = generalDisplayText,
                    IconLookupName = "Generic",
                    IconType = "generic"
                });
            }
            
            // Strip "Square" suffix if it is already present in the folder name to avoid duplicate suffixes
            if (monsterNameRaw.EndsWith("Square", StringComparison.OrdinalIgnoreCase))
            {
                monsterNameRaw = monsterNameRaw.Substring(0, monsterNameRaw.Length - "Square".Length);
            }

            // Add underscores before capitals (e.g., BlueSentinel -> Blue_Sentinel)
            string monsterNameFormatted = Regex.Replace(monsterNameRaw, @"(?<!^)(?=[A-Z])", "_");
            // Normalize multiple underscores (e.g. Ancient__Krug to Ancient_Krug)
            monsterNameFormatted = Regex.Replace(monsterNameFormatted, @"_+", "_").Trim('_');
            string displayMonsterName = monsterNameFormatted.Replace("_", " ");

            string displayText = _translationService.GetText(language, "interaction_attack_monster", 
                new Dictionary<string, string> { { "monster", displayMonsterName } });

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = monsterNameFormatted,
                IconType = "monster"
            });
        }
    }
}
