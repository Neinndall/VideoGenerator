using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public class ItemEventHandler : IEventHandler
    {
        private readonly TranslationService _translationService;

        public ItemEventHandler(TranslationService translationService)
        {
            _translationService = translationService;
        }

        public bool CanHandle(string folderName)
        {
            return folderName.Contains("BuyItem") || folderName.Contains("UseItem");
        }

        public async Task<ParsedEvent> HandleAsync(string folderName, string language)
        {
            // Replicating Python logic: item id is usually the number at the end
            var match = Regex.Match(folderName, @"(BuyItem|UseItem)(2D|3D)(.*)");
            if (!match.Success) return new ParsedEvent { OriginalFolder = folderName };

            string action = match.Groups[1].Value;
            string itemIdOrName = match.Groups[3].Value;

            string textKey = action == "BuyItem" ? "event_buy_item" : "event_use_item";
            // Use the itemIdOrName for translation display and icon lookup
            string displayText = _translationService.GetText(language, textKey, 
                new Dictionary<string, string> { { "item_name", itemIdOrName } });

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = itemIdOrName,
                IconType = "item"
            };
        }
    }
}
