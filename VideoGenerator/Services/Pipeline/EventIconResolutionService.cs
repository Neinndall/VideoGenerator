using System;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Resolves an event icon from its semantic category and lookup value.
    /// </summary>
    public sealed class EventIconResolutionService
    {
        private readonly DataFetcher _dataFetcher;
        private readonly IconManager _iconManager;
        private readonly LogService _logger;

        public EventIconResolutionService(DataFetcher dataFetcher, IconManager iconManager, LogService logger)
        {
            _dataFetcher = dataFetcher;
            _iconManager = iconManager;
            _logger = logger;
        }

        public Task<string> ResolveItemNameToIdAsync(string itemName)
        {
            return _dataFetcher.ResolveItemNameToIdAsync(itemName);
        }

        public async Task<string> ResolveAsync(ParsedEvent parsedEvent)
        {
            if (parsedEvent == null || parsedEvent.IconType == "generic")
            {
                return null;
            }

            try
            {
                string lolVersion = parsedEvent.IconType is "champion" or "region"
                    ? await _dataFetcher.GetLatestLolVersionAsync()
                    : null;

                return parsedEvent.IconType switch
                {
                    "champion" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                    "item" => await _iconManager.GetItemIconAsync(parsedEvent.IconLookupName),
                    "monster" => await _iconManager.GetMonsterIconAsync(parsedEvent.IconLookupName),
                    "region" => await _iconManager.GetChampionIconAsync(parsedEvent.IconLookupName, lolVersion),
                    "structure" => await _iconManager.GetStructureIconAsync(parsedEvent.IconLookupName),
                    "system" => await _iconManager.GetSystemIconAsync(parsedEvent.IconLookupName),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Icon resolution failed for {parsedEvent.IconType}:{parsedEvent.IconLookupName}", ex);
                return null;
            }
        }
    }
}
