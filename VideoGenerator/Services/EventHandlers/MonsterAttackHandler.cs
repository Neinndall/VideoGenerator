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
            return folderName.Contains("Attack2D") && !folderName.Contains("General");
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"Attack2D([A-Za-z]+)");
            if (!match.Success) return new ParsedEvent { OriginalFolder = folderName };

            string monsterNameRaw = match.Groups[1].Value;
            // Add underscores before capitals (e.g., BlueSentinel -> Blue_Sentinel)
            string monsterNameFormatted = Regex.Replace(monsterNameRaw, @"(?<!^)(?=[A-Z])", "_");
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
