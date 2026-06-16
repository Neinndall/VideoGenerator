using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Services.Parsers;

namespace VideoGenerator.Services
{
    public class NameParser
    {
        private readonly List<IEventParser> _parsers;

        public NameParser(
            TranslationService translationService, 
            DataFetcher dataFetcher, 
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager,
            SkinlineManager skinlineManager)
        {
            _parsers = new List<IEventParser>
            {
                new DynamicRuleParser(translationService, ruleManager, groupManager, aliasManager, skinlineManager),
                new ItemEventParser(translationService, dataFetcher),
                new MonsterEventParser(translationService),
                new SkinInteractionParser(translationService, dataFetcher, aliasManager),
                new SpellOrAttackParser(translationService)
            };
        }

        /// <summary>
        /// Main entrypoint to parse a folder name into a ParsedEvent.
        /// </summary>
        public async Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return CreateGenericEvent(folderName, string.Empty);
            }

            foreach (var parser in _parsers)
            {
                if (parser.CanParse(folderName))
                {
                    var parsed = await parser.ParseAsync(folderName, language);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
            }

            // Default fallback
            return CreateGenericEvent(folderName, folderName);
        }

        private ParsedEvent CreateGenericEvent(string folderName, string displayText)
        {
            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = "Generic",
                IconType = "generic"
            };
        }
    }
}
