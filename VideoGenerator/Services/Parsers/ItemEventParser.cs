using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public class ItemEventParser : IEventParser
    {
        private readonly TranslationService _translationService;

        private static readonly Dictionary<string, string> ItemNameToIdMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "EssenceReaver", "3508" },
            { "InfinityEdge", "3031" },
            { "KrakenSlayer", "6672" },
            { "RapidFirecannon", "3094" },
            { "GaleForce", "6671" },
            { "LordDominiksRegards", "3036" },
            { "Bloodthirster", "3072" },
            { "MortalReminder", "3033" },
            { "BladeOfTheRuinedKing", "3153" },
            { "GuinsoosRageblade", "3124" },
            { "RabadonsDeathcap", "3089" },
            { "ZhongyasHourglass", "3157" }, // Zhonya typo
            { "ZhonyasHourglass", "3157" }
        };

        public ItemEventParser(TranslationService translationService)
        {
            _translationService = translationService;
        }

        public bool CanParse(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return false;
            return folderName.Contains("BuyItem", StringComparison.OrdinalIgnoreCase) || 
                   folderName.Contains("UseItem", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"(BuyItem|UseItem)(2D|3D)?_?(.*)", RegexOptions.IgnoreCase);
            if (!match.Success) return Task.FromResult<ParsedEvent>(null);

            string action = match.Groups[1].Value;
            string itemIdOrName = match.Groups[3].Value.Trim('_');

            // Translate item clean name if matches our lookup
            string resolvedLookup = itemIdOrName;
            if (ItemNameToIdMap.TryGetValue(itemIdOrName, out string id))
            {
                resolvedLookup = id;
            }

            // Find key action
            string textKey = action.Equals("BuyItem", StringComparison.OrdinalIgnoreCase) ? "event_buy_item" : "event_use_item";
            
            // Format name to add spaces if it's text (e.g. EssenceReaver -> Essence Reaver)
            string displayItemName = itemIdOrName;
            if (!int.TryParse(itemIdOrName, out _))
            {
                displayItemName = Regex.Replace(itemIdOrName, @"(?<!^)(?=[A-Z])", " ");
            }

            string displayText = _translationService.GetText(language, textKey, 
                new Dictionary<string, string> { { "item_name", displayItemName } });

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = resolvedLookup,
                IconType = "item"
            });
        }
    }
}
