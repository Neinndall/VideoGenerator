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
        private readonly LocalGameIconManager _iconManager;
        private readonly LogService _logger;

        public EventIconResolutionService(DataFetcher dataFetcher, LocalGameIconManager iconManager, LogService logger)
        {
            _dataFetcher = dataFetcher;
            _iconManager = iconManager;
            _logger = logger;
        }

        public Task<string> ResolveItemNameToIdAsync(string itemName)
        {
            return _dataFetcher.ResolveItemNameToIdAsync(itemName);
        }

        public static bool RequiresIcon(ParsedEvent parsedEvent)
        {
            string iconType = parsedEvent?.IconType?.Trim();
            return !string.IsNullOrWhiteSpace(iconType) &&
                   !iconType.Equals("generic", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(parsedEvent.IconLookupName);
        }

        public async Task<string> ResolveAsync(ParsedEvent parsedEvent)
        {
            if (!RequiresIcon(parsedEvent))
                return null;

            // Imported/user-defined rules may vary in casing and whitespace.
            // Normalize routing without mutating the event's stored mapping.
            string iconType = parsedEvent.IconType?.Trim().ToLowerInvariant();

            try
            {
                return await _iconManager.FindIconAsync(iconType, parsedEvent.IconLookupName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Icon resolution failed for {parsedEvent.IconType}:{parsedEvent.IconLookupName}", ex);
                return null;
            }
        }
    }
}
