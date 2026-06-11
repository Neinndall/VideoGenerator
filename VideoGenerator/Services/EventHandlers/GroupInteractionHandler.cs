using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class GroupInteractionHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly GroupManager _groupManager;
        private static readonly Random _random = new();

        public GroupInteractionHandler(TranslationService translationService, GroupManager groupManager)
        {
            _translationService = translationService;
            _groupManager = groupManager;
        }

        public bool CanHandle(string folderName)
        {
            return _groupManager.Groups.Any(g => g.Category == "Class" && folderName.EndsWith(g.Name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            var matchedGroup = _groupManager.Groups
                .FirstOrDefault(g => g.Category == "Class" && folderName.EndsWith(g.Name, StringComparison.OrdinalIgnoreCase));

            if (matchedGroup == null) return new ParsedEvent { OriginalFolder = folderName };

            string actionPrefix = folderName.Substring(0, folderName.Length - matchedGroup.Name.Length);
            
            // Get random champion from group
            var champions = matchedGroup.GetChampionsList();
            string iconTarget = champions.Count > 0 ? champions[_random.Next(champions.Count)] : "General";

            string groupTranslationKey = $"class_{matchedGroup.Name.ToLower()}";
            string displayGroupName = _translationService.GetText(language, groupTranslationKey);

            string displayText;
            string iconType = "champion";

            if (actionPrefix.Contains("Kill"))
            {
                displayText = _translationService.GetText(language, "interaction_kill_class", displayGroupName);
            }
            else if (actionPrefix.Contains("Respawn"))
            {
                displayText = _translationService.GetText(language, "interaction_respawn_class", displayGroupName);
                iconTarget = "General";
                iconType = "generic";
            }
            else if (actionPrefix.Contains("FirstEncounter"))
            {
                displayText = _translationService.GetText(language, "interaction_first_encounter_class", displayGroupName);
            }
            else
            {
                displayText = $"{actionPrefix} {displayGroupName}";
                iconType = "generic";
            }

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconTarget,
                IconType = iconType
            };
        }
    }
}
