using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public class ItemEventParser : IEventParser
    {
        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;

        public ItemEventParser(TranslationService translationService, DataFetcher dataFetcher)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
        }

        public bool CanParse(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return false;
            return folderName.Contains("BuyItem", StringComparison.OrdinalIgnoreCase) || 
                   folderName.Contains("UseItem", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"(BuyItem|UseItem)(2D|3D)?_?(.*)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            string action = match.Groups[1].Value;
            string itemIdOrName = match.Groups[3].Value.Trim('_');

            // Strip a trailing single uppercase letter suffix that Riot adds to event folder names
            // (e.g. UseItem3DGuardianAngelR -> GuardianAngel) without breaking real names like BFSword
            if (itemIdOrName.Length > 1 &&
                char.IsUpper(itemIdOrName[^1]) &&
                itemIdOrName[..^1].Any(char.IsLower))
            {
                itemIdOrName = itemIdOrName[..^1];
            }

            // Resolve item info from cached CommunityDragon items_data.json
            var itemInfo = await _dataFetcher.GetItemInfoAsync(itemIdOrName);
            string resolvedLookup = itemInfo?.Id.ToString() ?? itemIdOrName;
            string displayItemName = itemInfo?.Name ?? Regex.Replace(itemIdOrName, @"(?<!^)(?=[A-Z])", " ");

            // Find key action
            string textKey = action.Equals("BuyItem", StringComparison.OrdinalIgnoreCase) ? "event_buy_item" : "event_use_item";

            string displayText = _translationService.GetText(language, textKey, 
                new Dictionary<string, string> { { "item_name", displayItemName } });

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = resolvedLookup,
                IconType = "item"
            };
        }
    }
}
