using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class MappedInteractionHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly GroupManager _groupManager;
        private readonly Dictionary<string, string> _interactionMap;
        private readonly string _iconType;
        private static readonly Random _random = new();

        public MappedInteractionHandler(
            TranslationService translationService, 
            GroupManager groupManager,
            Dictionary<string, string> interactionMap, 
            string iconType = "generic")
        {
            _translationService = translationService;
            _groupManager = groupManager;
            _interactionMap = interactionMap;
            _iconType = iconType;
        }

        public bool CanHandle(string folderName)
        {
            return _interactionMap.Keys.Any(key => folderName.Contains(key, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            string foundKey = _interactionMap.Keys
                .OrderByDescending(k => k.Length)
                .FirstOrDefault(key => folderName.Contains(key, StringComparison.OrdinalIgnoreCase));

            if (foundKey == null) return new ParsedEvent { OriginalFolder = folderName };

            string baseTextKey = _interactionMap[foundKey];
            
            // Extract suffix after the key
            int index = folderName.IndexOf(foundKey, StringComparison.OrdinalIgnoreCase);
            string targetSuffix = folderName.Substring(index + foundKey.Length);

            // Clean up target suffix (e.g., 2DGeneral -> General)
            targetSuffix = Regex.Replace(targetSuffix, @"^(2D|3D)", "");

            string iconTarget = "General";
            string iconType = _iconType;
            string displayText;

            if (targetSuffix == "General" || string.IsNullOrEmpty(targetSuffix))
            {
                if (baseTextKey.Contains("first_encounter"))
                {
                    displayText = _translationService.GetText(language, "event_first_encounter_general");
                }
                else
                {
                    displayText = _translationService.GetText(language, baseTextKey, "General");
                }
            }
            else
            {
                // Format name
                string cleanedTargetName = Regex.Replace(targetSuffix, @"(?<!^)(?=[A-Z])", " ").Trim();
                
                // Check dynamic groups from GroupManager
                var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(cleanedTargetName, StringComparison.OrdinalIgnoreCase));
                
                if (matchedGroup != null)
                {
                    var candidates = matchedGroup.GetChampionsList();
                    iconTarget = candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : "General";
                    iconType = "champion";

                    string themeKey = $"{matchedGroup.Category.ToLower()}_{cleanedTargetName.ToLower().Replace(" ", "_")}";
                    string displaySkinName = _translationService.GetText(language, themeKey);
                    displayText = _translationService.GetText(language, baseTextKey, displaySkinName);
                }
                else
                {
                    displayText = _translationService.GetText(language, baseTextKey, targetSuffix);
                    iconTarget = targetSuffix;
                    iconType = "champion";
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
