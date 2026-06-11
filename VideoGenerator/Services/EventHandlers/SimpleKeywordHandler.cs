using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class SimpleKeywordHandler : IEventHandler
    {
        private readonly TranslationService _translationService;
        private readonly Dictionary<string, (string textKey, string iconTarget, string iconType)> _keywordMap;
        
        public SimpleKeywordHandler(TranslationService translationService, Dictionary<string, (string, string, string)> keywordMap)
        {
            _translationService = translationService;
            _keywordMap = keywordMap;
        }

        public bool CanHandle(string folderName)
        {
            return _keywordMap.Keys.Any(k => folderName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            string foundKey = _keywordMap.Keys.FirstOrDefault(k => folderName.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (foundKey == null) return new ParsedEvent { OriginalFolder = folderName };

            var (textKey, iconTarget, iconType) = _keywordMap[foundKey];

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = _translationService.GetText(language, textKey),
                IconLookupName = iconTarget,
                IconType = iconType
            };
        }
    }
}
