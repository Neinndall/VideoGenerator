using System;
using System.Linq;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services.Parsers
{
    public class GroupInteractionParser : IEventParser
    {
        private readonly TranslationService _translationService;
        private readonly GroupManager _groupManager;
        private static readonly Random _random = new();

        public GroupInteractionParser(
            TranslationService translationService,
            GroupManager groupManager)
        {
            _translationService = translationService;
            _groupManager = groupManager;
        }

        public bool CanParse(string folderName)
        {
            return FindGroupClassMatch(folderName) != null;
        }

        public Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            var matchedGroup = FindGroupClassMatch(folderName);
            if (matchedGroup == null) return Task.FromResult<ParsedEvent>(null);

            string actionPrefix = folderName.Substring(0, folderName.Length - matchedGroup.Name.Length);
            
            // Get random champion from group for the preview icon
            var champions = matchedGroup.GetChampionsList();
            string iconTarget = champions.Count > 0 ? champions[_random.Next(champions.Count)] : "General";

            string groupTranslationKey = $"class_{matchedGroup.Name.ToLower()}";
            string displayGroupName = _translationService.GetText(language, groupTranslationKey);

            string displayText;
            string iconType = "champion";

            if (actionPrefix.Contains("Kill", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_kill_class", displayGroupName);
            }
            else if (actionPrefix.Contains("Respawn", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_respawn_class", displayGroupName);
                iconTarget = "General";
                iconType = "generic";
            }
            else if (actionPrefix.Contains("FirstEncounter", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_first_encounter_class", displayGroupName);
            }
            else
            {
                displayText = $"{actionPrefix} {displayGroupName}";
                iconType = "generic";
            }

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconTarget,
                IconType = iconType
            });
        }

        private ThematicGroup FindGroupClassMatch(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return null;
            return _groupManager.Groups.FirstOrDefault(g => 
                g.Category == "Class" && 
                folderName.EndsWith(g.Name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
