using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Services.Parsers;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class EventNameParser : IEventNameParser
    {
        private readonly List<IEventParser> _parsers;

        public EventNameParser(
            TranslationService translationService, 
            DataFetcher dataFetcher, 
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager,
            SkinlineManager skinlineManager)
        {
            var monsterParser = new MonsterEventParser(translationService, dataFetcher, aliasManager);
            _parsers = new List<IEventParser>
            {
                new ItemEventParser(translationService, dataFetcher),
                monsterParser,
                new SkinInteractionParser(translationService, dataFetcher, aliasManager),
                new SpellOrAttackParser(translationService),
                new DynamicRuleParser(translationService, dataFetcher, ruleManager, groupManager, aliasManager, skinlineManager, monsterParser)
            };
        }

        /// <summary>
        /// Main entrypoint to parse a folder name into a ParsedEvent.
        /// Uses explicit routing rules to avoid order-dependent conflicts.
        /// </summary>
        public async Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return CreateGenericEvent(folderName, string.Empty);
            }

            string cleanFolder = StripOwnerPrefix(folderName);

            // 1. Route to ItemEventParser if it matches the signatures explicitly
            if (folderName.Contains("BuyItem", StringComparison.OrdinalIgnoreCase) || 
                folderName.Contains("UseItem", StringComparison.OrdinalIgnoreCase))
            {
                var itemParser = _parsers.OfType<ItemEventParser>().FirstOrDefault();
                if (itemParser != null)
                {
                    var result = await itemParser.ParseAsync(folderName, language);
                    if (result != null) return MarkMapped(result);
                }
            }

            // 2. Route to SkinInteractionParser if the target part contains target skin signatures
            if (Regex.IsMatch(cleanFolder, @"[A-Za-z]+Skin\d+", RegexOptions.IgnoreCase))
            {
                var skinParser = _parsers.OfType<SkinInteractionParser>().FirstOrDefault();
                if (skinParser != null)
                {
                    var result = await skinParser.ParseAsync(folderName, language);
                    if (result != null) return MarkMapped(result);
                }
            }

            // 3. Route to MonsterEventParser if it explicitly targets a monster
            if (folderName.Contains("Attack2D", StringComparison.OrdinalIgnoreCase) || 
                folderName.Contains("Attack3D", StringComparison.OrdinalIgnoreCase))
            {
                var monsterParser = _parsers.OfType<MonsterEventParser>().FirstOrDefault();
                if (monsterParser != null)
                {
                    var result = await monsterParser.ParseAsync(folderName, language);
                    if (result != null) return MarkMapped(result);
                }
            }

            // 4. The rule engine owns matching AND parsing. Previously there
            // were two near-identical keyword matchers (here and inside
            // DynamicRuleParser), followed by a redundant second invocation.
            // They could disagree about custom/official rules.
            var dynamicParser = _parsers.OfType<DynamicRuleParser>().FirstOrDefault();
            if (dynamicParser != null)
            {
                var result = await dynamicParser.ParseAsync(folderName, language);
                if (result != null) return MarkMapped(result);
            }

            // A targeted attack without a recognized monster or explicit rule needs review.
            // SpellOrAttackParser's English fallback would otherwise mark it as mapped.
            if (Regex.IsMatch(cleanFolder, @"^Attack(?:2D|3D)_?[A-Za-z0-9_]+$", RegexOptions.IgnoreCase))
            {
                return CreateGenericEvent(folderName, folderName);
            }

            // 5. Route to SpellOrAttackParser for custom abilities or hits that didn't match a rule keyword
            var spellParser = _parsers.OfType<SpellOrAttackParser>().FirstOrDefault();
            if (spellParser != null && spellParser.CanParse(folderName))
            {
                var result = await spellParser.ParseAsync(folderName, language);
                if (result != null) return MarkMapped(result);
            }

            return CreateGenericEvent(folderName, folderName);
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string pattern = @"^(_)?(Play_vo_|Play_|vo_|Play_vo_)([A-Za-z0-9]+?)(Skin\d+)?_";
            string stripped = Regex.Replace(folderName, pattern, "", RegexOptions.IgnoreCase);
            return stripped.TrimStart('_');
        }

        private ParsedEvent CreateGenericEvent(string folderName, string displayText)
        {
            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IsMapped = false,
                IconLookupName = "Generic",
                IconType = "generic"
            };
        }

        private static ParsedEvent MarkMapped(ParsedEvent parsedEvent)
        {
            parsedEvent.IsMapped = true;
            return parsedEvent;
        }
    }
}
