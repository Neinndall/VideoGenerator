using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class DynamicRuleHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly EventRule _rule;
        private readonly GroupManager _groupManager;

        public DynamicRuleHandler(TranslationService translationService, EventRule rule, GroupManager groupManager)
        {
            _translationService = translationService;
            _rule = rule;
            _groupManager = groupManager;
        }

        public bool CanHandle(string folderName) => folderName.Contains(_rule.Keyword, StringComparison.OrdinalIgnoreCase);

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            if (_rule.Type == RuleType.Simple)
            {
                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = _translationService.GetText(language, _rule.TranslationKey),
                    IconLookupName = "Generic",
                    IconType = _rule.IconType
                };
            }

            // Extraction Logic
            int index = folderName.IndexOf(_rule.Keyword, StringComparison.OrdinalIgnoreCase);
            string targetName = folderName.Substring(index + _rule.Keyword.Length);
            
            // Cleanup target name (remove 2D/3D)
            targetName = Regex.Replace(targetName, @"^(2D|3D)", "");

            string iconTarget = string.IsNullOrEmpty(targetName) ? "General" : targetName;
            string iconType = _rule.IconType;
            string displayText;

            if (_rule.Type == RuleType.Interaction && (iconTarget == "General" || string.IsNullOrEmpty(iconTarget)))
            {
                // Specialized fallback for interactions (like FirstEncounterGeneral)
                if (_rule.TranslationKey.Contains("first_encounter"))
                {
                    displayText = _translationService.GetText(language, "event_first_encounter_general");
                }
                else
                {
                    displayText = _translationService.GetText(language, _rule.TranslationKey, "General");
                }
            }
            else
            {
                // Format targetName for display (e.g., "TahmKench" -> "Tahm Kench")
                string displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();

                // Check dynamic groups from GroupManager
                var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(displayTargetName, StringComparison.OrdinalIgnoreCase));
                if (_rule.Type == RuleType.Interaction && matchedGroup != null)
                {
                    var candidates = matchedGroup.GetChampionsList();
                    iconTarget = candidates.Count > 0 ? candidates[new Random().Next(candidates.Count)] : "General";
                    iconType = "champion";

                    string themeKey = $"{matchedGroup.Category.ToLower()}_{displayTargetName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    displayText = _translationService.GetText(language, _rule.TranslationKey, themeDisplayName);
                }
                else
                {
                    displayText = _translationService.GetText(language, _rule.TranslationKey, displayTargetName);
                }
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
