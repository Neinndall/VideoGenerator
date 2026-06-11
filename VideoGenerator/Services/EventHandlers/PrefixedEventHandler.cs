using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class PrefixedEventHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly string _prefix;
        private readonly string _textKey;
        private readonly string _iconType;

        public PrefixedEventHandler(TranslationService translationService, string prefix, string textKey, string iconType)
        {
            _translationService = translationService;
            _prefix = prefix;
            _textKey = textKey;
            _iconType = iconType;
        }

        public bool CanHandle(string folderName)
        {
            return folderName.Contains(_prefix, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            int index = folderName.IndexOf(_prefix, StringComparison.OrdinalIgnoreCase);
            string targetName = folderName.Substring(index + _prefix.Length);
            
            // Cleanup target name (remove 2D/3D)
            targetName = Regex.Replace(targetName, @"^(2D|3D)", "");

            if (_prefix == "Kill" && targetName == "General")
            {
                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = _translationService.GetText(language, "event_kill_general"),
                    IconLookupName = "General",
                    IconType = "generic"
                };
            }

            string iconTarget = string.IsNullOrEmpty(targetName) ? "General" : targetName;
            
            // Format targetName for display (e.g., "TahmKench" -> "Tahm Kench")
            string displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = _translationService.GetText(language, _textKey, displayTargetName),
                IconLookupName = iconTarget, // Keep raw for icon searching
                IconType = _iconType
            };
        }
    }
}
