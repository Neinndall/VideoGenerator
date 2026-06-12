using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class MonsterAttackHandler : IEventHandler
    {
        private readonly TranslationService _translationService;

        public MonsterAttackHandler(TranslationService translationService)
        {
            _translationService = translationService;
        }

        public bool CanHandle(string folderName)
        {
            return folderName.Contains("Attack2D");
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"Attack2D([A-Za-z0-9_]+)");
            if (!match.Success) return new ParsedEvent { OriginalFolder = folderName };

            string monsterNameRaw = match.Groups[1].Value;

            if (monsterNameRaw.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                string actionText = _translationService.GetText(language, "event_attack");
                string suffixText = _translationService.GetText(language, "suffix_in_general");
                string generalDisplayText = $"{actionText}{suffixText}";

                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = generalDisplayText,
                    IconLookupName = "Generic",
                    IconType = "generic"
                };
            }
            
            // Strip "Square" suffix if it is already present in the folder name to avoid duplicate suffixes
            if (monsterNameRaw.EndsWith("Square", StringComparison.OrdinalIgnoreCase))
            {
                monsterNameRaw = monsterNameRaw.Substring(0, monsterNameRaw.Length - "Square".Length);
            }

            // Add underscores before capitals (e.g., BlueSentinel -> Blue_Sentinel)
            string monsterNameFormatted = Regex.Replace(monsterNameRaw, @"(?<!^)(?=[A-Z])", "_");
            // Normalize multiple underscores (e.g. Ancient__Krug to Ancient_Krug)
            monsterNameFormatted = Regex.Replace(monsterNameFormatted, @"_+", "_");
            string displayMonsterName = monsterNameFormatted.Replace("_", " ");

            string displayText = _translationService.GetText(language, "interaction_attack_monster", 
                new Dictionary<string, string> { { "monster", displayMonsterName } });

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = monsterNameFormatted,
                IconType = "monster"
            };
        }
    }
}
