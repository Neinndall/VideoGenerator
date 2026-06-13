using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Services.EventHandlers;

namespace VideoGenerator.Services
{
    public class NameParser
    {
        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;
        private readonly RuleManager _ruleManager;
        private readonly GroupManager _groupManager;

        public NameParser(
            TranslationService translationService, 
            DataFetcher dataFetcher, 
            RuleManager ruleManager,
            GroupManager groupManager)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            _ruleManager = ruleManager;
            _groupManager = groupManager;
        }

        private List<IEventHandler> GetActiveHandlers()
        {
            var handlers = new List<IEventHandler>();

            // 1. Add Complex Specialized Handlers first
            handlers.Add(new ItemEventHandler(_translationService));
            handlers.Add(new MonsterAttackHandler(_translationService));
            handlers.Add(new SkinInteractionHandler(_translationService, _dataFetcher));
            handlers.Add(new GroupInteractionHandler(_translationService, _groupManager));

            // 2. Load Dynamic User Rules sorted by keyword length descending (Fallback/Catch-all)
            // This prevents short generic keywords (e.g. "Attack") from greedy matching over longer specific keywords (e.g. "Attack2D")
            foreach (var rule in _ruleManager.Rules.OrderByDescending(r => r.Keyword.Length))
            {
                handlers.Add(new DynamicRuleHandler(_translationService, rule, _groupManager));
            }

            return handlers;
        }

        public async Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language)
        {
            var handlers = GetActiveHandlers();

            foreach (var handler in handlers)
            {
                if (handler.CanHandle(folderName))
                {
                    return await handler.HandleAsync(folderName, language);
                }
            }

            // Default fallback
            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = folderName,
                IconLookupName = "Generic",
                IconType = "generic"
            };
        }
    }
}
